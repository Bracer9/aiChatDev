namespace NichiryuSim;

public sealed class CharacterService(ContentService content)
{
    private const int MaxMemoriesPerCharacter = 24;

    public CharacterRelationship EnsureRelationship(GameState state, string characterId)
    {
        var character = content.Character(characterId);
        if (state.Relationships.TryGetValue(characterId, out var existing))
        {
            Hydrate(existing, character);
            return existing;
        }

        var relationship = new CharacterRelationship
        {
            CharacterId = character.Id,
            Name = character.Name,
            Stage = character.Stage,
            Affection = character.Affection,
            Trust = character.Trust,
            Mood = character.InitialMood,
            MoodValue = character.InitialMoodValue
        };
        state.Relationships[characterId] = relationship;
        return relationship;
    }

    public RelationshipInteractionPayload ResolveInteraction(GameState state, string characterId, string actionId)
    {
        var character = content.Character(characterId);
        var relationship = EnsureRelationship(state, characterId);
        var preference = character.InteractionPreferences.GetValueOrDefault(actionId) ?? new();
        var (baseAffection, baseTrust) = actionId == "support" ? (4, 7) : (6, 3);
        var affectionDelta = Math.Max(1, (int)Math.Round(baseAffection * preference.AffectionMultiplier));
        var trustDelta = Math.Max(1, (int)Math.Round(baseTrust * preference.TrustMultiplier));
        var stageBefore = relationship.Stage;

        relationship.Affection += affectionDelta;
        relationship.Trust += trustDelta;
        relationship.MoodValue = Math.Clamp(relationship.MoodValue + preference.MoodDelta, -100, 100);
        relationship.Mood = MoodFromValue(relationship.MoodValue);
        relationship.InteractionCount++;
        relationship.LastInteractionMonth = state.CurrentMonth;
        relationship.LastActionId = actionId;

        foreach (var (category, value) in preference.CoreDelta)
            state.Core.Add(category, value);
        foreach (var flag in preference.SetFlags)
            state.Flags[flag] = true;

        UpdateStageAndFlags(state, character, relationship);
        var interactionId = $"interaction_{state.CurrentMonth:00}_{characterId}_{relationship.InteractionCount:00}";
        AddMemory(relationship, new()
        {
            Id = interactionId,
            Month = state.CurrentMonth,
            Type = "interaction",
            Title = actionId == "support" ? "一次认真倾听" : "一次主动闲聊",
            Summary = actionId == "support"
                ? $"你认真听完了{character.Name}想说的事。"
                : $"你主动与{character.Name}聊了一会儿。",
            Importance = Math.Clamp(preference.MemoryImportance, 1, 5),
            ActionId = actionId,
            Tags = preference.MemoryTags.ToList()
        });

        return new()
        {
            Source = "pending",
            InteractionId = interactionId,
            CharacterId = characterId,
            ActionId = actionId,
            AffectionDelta = affectionDelta,
            TrustDelta = trustDelta,
            StageBefore = stageBefore,
            StageAfter = relationship.Stage,
            Title = "正在整理这次相处",
            SceneText = "AI 正在根据人物档案、当前情绪和共同记忆生成互动场景。",
            Mood = relationship.Mood
        };
    }

    public void RecordEventMemories(GameState state, EventDefinition definition)
    {
        foreach (var characterId in definition.RelatedCharacterIds)
        {
            var relationship = EnsureRelationship(state, characterId);
            AddMemory(relationship, new()
            {
                Id = $"event_{definition.Id}",
                Month = state.CurrentMonth,
                Type = "event",
                Title = definition.Name,
                Summary = definition.Description,
                Importance = 3,
                EventId = definition.Id,
                Tags = ["共同事件"]
            });
        }
    }

    public void EnrichLatestInteractionMemory(GameState state, RelationshipInteractionPayload payload)
    {
        if (!state.Relationships.TryGetValue(payload.CharacterId, out var relationship)) return;
        var memory = relationship.Memories.FirstOrDefault(x => x.Id == payload.InteractionId);
        if (memory is null) return;

        memory.Title = payload.Title;
        var summary = string.IsNullOrWhiteSpace(payload.MemoryUpdate) ? payload.ResultText : payload.MemoryUpdate;
        if (string.IsNullOrWhiteSpace(summary)) summary = payload.SceneText;
        memory.Summary = summary.Length <= 220 ? summary : summary[..220] + "……";
        if (!string.IsNullOrWhiteSpace(payload.Mood) && !memory.Tags.Contains(payload.Mood))
            memory.Tags.Add(payload.Mood);
    }

    public void ApplyInteractionChoice(
        GameState state,
        RelationshipInteractionPayload payload,
        VisualNovelInteractionOption option)
    {
        var relationship = EnsureRelationship(state, payload.CharacterId);
        var character = content.Character(payload.CharacterId);
        var delta = option.AffectionDelta is 1 or 3 ? option.AffectionDelta : 0;

        relationship.Affection += delta;
        payload.SelectedOptionId = option.OptionId;
        payload.ChoiceAffectionDelta = delta;
        payload.ChoiceResultText = option.ResultText;
        payload.AffectionDelta += delta;
        UpdateStageAndFlags(state, character, relationship);
        payload.StageAfter = relationship.Stage;

        var memory = relationship.Memories.FirstOrDefault(x => x.Id == payload.InteractionId);
        if (memory is null) return;
        var choiceSummary = $"你的回应：{option.Text}";
        memory.Summary = string.IsNullOrWhiteSpace(memory.Summary)
            ? choiceSummary
            : $"{memory.Summary} {choiceSummary}";
        if (memory.Summary.Length > 220)
            memory.Summary = memory.Summary[..220] + "……";
    }

    public void HydrateState(GameState state)
    {
        foreach (var relationship in state.Relationships.Values)
            Hydrate(relationship, content.Character(relationship.CharacterId));
    }

    public void UpdateStageAndFlags(GameState state, CharacterContent character, CharacterRelationship relationship)
    {
        if (relationship.Stage == "acquaintance" && relationship.Affection >= 20 && relationship.Trust >= 10)
            relationship.Stage = "friend";
        if (relationship.Stage == "friend" && relationship.Affection >= 45 && relationship.Trust >= 35)
            relationship.Stage = "close";

        if (character.StageSetFlags.TryGetValue(relationship.Stage, out var flags))
            foreach (var flag in flags)
                state.Flags[flag] = true;
    }

    private static void AddMemory(CharacterRelationship relationship, CharacterMemory memory)
    {
        if (relationship.Memories.Any(x => x.Id == memory.Id)) return;
        relationship.Memories.Add(memory);
        if (relationship.Memories.Count <= MaxMemoriesPerCharacter) return;

        var removable = relationship.Memories
            .Select((item, index) => new { item, index })
            .Where(x => x.item.Importance < 4)
            .OrderBy(x => x.item.Importance)
            .ThenBy(x => x.item.Month)
            .FirstOrDefault();
        relationship.Memories.RemoveAt(removable?.index ?? 0);
    }

    private static void Hydrate(CharacterRelationship relationship, CharacterContent character)
    {
        relationship.Name = string.IsNullOrWhiteSpace(relationship.Name) ? character.Name : relationship.Name;
        relationship.Mood = string.IsNullOrWhiteSpace(relationship.Mood) ? character.InitialMood : relationship.Mood;
        relationship.Memories ??= [];
    }

    private static string MoodFromValue(int value) => value switch
    {
        >= 45 => "warm",
        >= 15 => "open",
        <= -35 => "upset",
        <= -10 => "guarded",
        _ => "neutral"
    };
}
