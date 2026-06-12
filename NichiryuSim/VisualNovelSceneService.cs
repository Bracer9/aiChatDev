namespace NichiryuSim;

public sealed class VisualNovelSceneService(IWebHostEnvironment environment, ContentService content)
{
    private const string DefaultNpcPortraitPath = "/assets/portraits/default_npc.png";
    private readonly string _webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");

    public RelationshipInteractionPayload Normalize(
        RelationshipInteractionPayload payload,
        CharacterContent character,
        RelationshipInteractionPayload resolved)
    {
        payload.Type = "visual_novel_scene";
        payload.InteractionId = resolved.InteractionId;
        payload.SceneId = string.IsNullOrWhiteSpace(payload.SceneId) ? resolved.InteractionId : payload.SceneId.Trim();
        payload.CharacterId = resolved.CharacterId;
        payload.ActionId = resolved.ActionId;
        payload.AffectionDelta = resolved.AffectionDelta;
        payload.TrustDelta = resolved.TrustDelta;
        payload.StageBefore = resolved.StageBefore;
        payload.StageAfter = resolved.StageAfter;

        var background = ResolveBackground(
            character,
            string.IsNullOrWhiteSpace(resolved.BackgroundId) ? payload.BackgroundId : resolved.BackgroundId);
        payload.BackgroundId = background.Id;
        payload.BackgroundPath = background.BackgroundPath;

        payload.Characters =
        [
            ResolveCharacter(character)
        ];

        payload.Lines = NormalizeLines(payload.Lines, character, payload.ActionId, background);
        if (payload.Lines.Count == 0)
            payload.Lines = CreateFallbackLines(character, payload.ActionId, background);
        payload.InteractionOptions = NormalizeInteractionOptions(payload.InteractionOptions, character, payload.ActionId);
        payload.SelectedOptionId = null;
        payload.ChoiceAffectionDelta = 0;
        payload.ChoiceResultText = "";

        payload.Title = string.IsNullOrWhiteSpace(payload.Title) ? "这次相处留下的片段" : payload.Title.Trim();
        payload.Mood = string.IsNullOrWhiteSpace(payload.Mood) ? "平静" : payload.Mood.Trim();
        payload.ResultText = string.IsNullOrWhiteSpace(payload.ResultText)
            ? "这次交谈没有立刻改变什么，但你们之间多了一段可以被记住的时间。"
            : payload.ResultText.Trim();
        payload.MemoryUpdate = string.IsNullOrWhiteSpace(payload.MemoryUpdate)
            ? payload.ResultText
            : payload.MemoryUpdate.Trim();
        payload.SceneText = string.Join("\n", payload.Lines.Select(x => x.Text));
        return payload;
    }

    private static List<VisualNovelInteractionOption> NormalizeInteractionOptions(
        IEnumerable<VisualNovelInteractionOption>? source,
        CharacterContent character,
        string actionId)
    {
        var options = (source ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .Take(2)
            .Select((x, index) => new VisualNovelInteractionOption
            {
                OptionId = $"option_{index + 1}",
                Text = x.Text.Trim(),
                AffectionDelta = NormalizeChoiceAffection(x.AffectionDelta),
                ResultText = string.IsNullOrWhiteSpace(x.ResultText)
                    ? $"{character.Name}认真听完了你的回应。"
                    : x.ResultText.Trim()
            })
            .ToList();

        if (options.Count > 0) return options;
        return
        [
            new()
            {
                OptionId = "option_1",
                Text = actionId == "support" ? "先不急着给建议，表示愿意继续听。" : "自然地回应刚才的话题。",
                AffectionDelta = 0,
                ResultText = $"{character.Name}接受了这份不过界的回应。"
            },
            new()
            {
                OptionId = "option_2",
                Text = actionId == "support" ? "记住对方在意的细节，认真回应。" : "顺着对方真正感兴趣的部分继续聊。",
                AffectionDelta = 1,
                ResultText = $"{character.Name}注意到你确实听进了刚才的话。"
            }
        ];
    }

    private static int NormalizeChoiceAffection(int value) => value switch
    {
        >= 3 => 3,
        >= 1 => 1,
        _ => 0
    };

    private List<VisualNovelLine> NormalizeLines(
        IEnumerable<VisualNovelLine>? source,
        CharacterContent character,
        string actionId,
        SceneContent background)
    {
        var lines = new List<VisualNovelLine>();
        foreach (var line in source ?? [])
        {
            var text = (line.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            var lineType = line.LineType is "npc" or "player" or "narration" ? line.LineType : "narration";
            var expression = NormalizeExpression(line.Expression);
            var portrait = lineType == "npc" ? ResolvePortrait(character, expression) : null;
            var lineBackground = string.IsNullOrWhiteSpace(line.BackgroundId)
                ? background
                : ResolveBackground(character, line.BackgroundId, background);
            lines.Add(new()
            {
                LineType = lineType,
                SpeakerId = lineType switch
                {
                    "npc" => character.Id,
                    "player" => "player",
                    _ => "narrator"
                },
                SpeakerName = lineType switch
                {
                    "npc" => character.Name,
                    "player" => "你",
                    _ => "旁白"
                },
                Text = text,
                BackgroundId = lineBackground.Id,
                BackgroundPath = lineBackground.BackgroundPath,
                PortraitId = portrait?.PortraitId,
                PortraitPath = portrait?.PortraitPath,
                Expression = lineType == "npc" ? expression : "neutral"
            });
        }
        return lines.Take(18).ToList();
    }

    private VisualNovelCharacter ResolveCharacter(CharacterContent character)
    {
        var portrait = ResolvePortrait(character, "neutral");
        return new()
        {
            CharacterId = character.Id,
            DisplayName = character.Name,
            PortraitId = portrait.PortraitId,
            PortraitPath = portrait.PortraitPath
        };
    }

    private CharacterPortraitContent ResolvePortrait(CharacterContent character, string expression)
    {
        if (character.Portraits.TryGetValue(expression, out var portrait) && IsUsablePortrait(portrait))
            return portrait;
        if (character.Portraits.TryGetValue("neutral", out portrait) && IsUsablePortrait(portrait))
            return portrait;

        var fallback = new CharacterPortraitContent
        {
            PortraitId = string.IsNullOrWhiteSpace(character.DefaultPortraitId) ? "default_npc" : character.DefaultPortraitId,
            PortraitPath = string.IsNullOrWhiteSpace(character.DefaultPortraitPath) ? DefaultNpcPortraitPath : character.DefaultPortraitPath
        };
        return IsUsablePortrait(fallback)
            ? fallback
            : new() { PortraitId = "default_npc", PortraitPath = DefaultNpcPortraitPath };
    }

    private SceneContent ResolveBackground(CharacterContent character, string? requestedId, SceneContent? fallback = null)
    {
        if (!string.IsNullOrWhiteSpace(requestedId))
        {
            var requested = content.Scenes.FirstOrDefault(x =>
                string.Equals(x.Id, requestedId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (requested is not null) return requested;
        }

        if (fallback is not null) return fallback;
        var characterDefault = content.Scenes.FirstOrDefault(x =>
            string.Equals(x.Id, character.DefaultSceneId, StringComparison.OrdinalIgnoreCase));
        return characterDefault ?? content.DefaultScene();
    }

    private static string NormalizeExpression(string? expression) => expression?.Trim().ToLowerInvariant() switch
    {
        "happy" or "joy" or "喜" or "喜悦" or "开心" => "happy",
        "angry" or "怒" or "生气" => "angry",
        "sad" or "哀" or "悲伤" => "sad",
        "surprised" or "惊" or "惊讶" => "surprised",
        "calm" or "平静" => "calm",
        _ => "neutral"
    };

    private List<VisualNovelLine> CreateFallbackLines(
        CharacterContent character,
        string actionId,
        SceneContent background)
    {
        var portrait = ResolvePortrait(character, "neutral");
        return
        [
            new()
            {
                LineType = "narration",
                SpeakerId = "narrator",
                SpeakerName = "旁白",
                Text = actionId == "support"
                    ? "你没有急着给出建议，只是留在原地，等对方把想说的话慢慢整理出来。"
                    : "课程结束后，你在走廊叫住了对方。短暂的停顿里，周围的脚步声渐渐远去。",
                BackgroundId = background.Id,
                BackgroundPath = background.BackgroundPath
            },
            new()
            {
                LineType = "npc",
                SpeakerId = character.Id,
                SpeakerName = character.Name,
                Text = actionId == "support" ? "谢谢你听我说完。这样已经帮了很大的忙。" : "原来你也注意到了那件事。我还以为只有我会在意。",
                BackgroundId = background.Id,
                BackgroundPath = background.BackgroundPath,
                PortraitId = portrait.PortraitId,
                PortraitPath = portrait.PortraitPath,
                Expression = "neutral"
            },
            new()
            {
                LineType = "narration",
                SpeakerId = "narrator",
                SpeakerName = "旁白",
                Text = "对方说完后没有立刻移开视线。窗外的声音填进短暂的停顿，让这句话显得比平时更认真。",
                BackgroundId = background.Id,
                BackgroundPath = background.BackgroundPath
            },
            new()
            {
                LineType = "player",
                SpeakerId = "player",
                SpeakerName = "你",
                Text = actionId == "support" ? "不用急着得出答案。你想说的时候，我会听。" : "下次有空的话，我们可以继续聊。",
                BackgroundId = background.Id,
                BackgroundPath = background.BackgroundPath
            },
            new()
            {
                LineType = "npc",
                SpeakerId = character.Id,
                SpeakerName = character.Name,
                Text = actionId == "support" ? "嗯。下次如果你也有想说的事，可以来找我。" : "好。那就说定了，别到时候又被别的安排挤掉。",
                BackgroundId = background.Id,
                BackgroundPath = background.BackgroundPath,
                PortraitId = portrait.PortraitId,
                PortraitPath = portrait.PortraitPath,
                Expression = actionId == "support" ? "calm" : "happy"
            }
        ];
    }

    private bool IsUsablePortrait(CharacterPortraitContent portrait)
    {
        if (string.IsNullOrWhiteSpace(portrait.PortraitPath)) return false;
        if (Uri.TryCreate(portrait.PortraitPath, UriKind.Absolute, out var uri) && !uri.IsFile)
            return true;

        var relative = portrait.PortraitPath
            .Trim()
            .TrimStart('/', '\\')
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        return File.Exists(Path.Combine(_webRoot, relative));
    }
}
