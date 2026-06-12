namespace NichiryuSim;

public sealed class CardService(ContentService content)
{
    public string? GetUnlockReason(CardDefinition card, GameState state)
    {
        if (card.IsInitialCard || card.InitialFacultyIds.Contains(state.FacultyId))
            return null;

        var requirements = card.UnlockRequirements;
        var missingLevels = requirements.RequiredCoreAttributeLevels
            .Where(x => state.Core.Level(x.Key) < x.Value)
            .Select(x => $"{content.Label(x.Key)} Lv{x.Value}")
            .ToList();
        if (missingLevels.Count > 0) return $"需要核心属性：{string.Join(" / ", missingLevels)}";

        var missingFlags = requirements.RequiredFlags.Where(x => !state.Flags.GetValueOrDefault(x)).ToList();
        if (missingFlags.Count > 0) return $"需要事件条件：{string.Join(" / ", missingFlags)}";
        if (requirements.ForbiddenFlags.Any(x => state.Flags.GetValueOrDefault(x)))
            return "当前状态下不会出现。";
        if (requirements.RequiredFaculty is { } faculty && state.FacultyId != faculty)
            return $"仅限学部：{content.Faculty(faculty).Name}";
        if (requirements.RequiredSeminar is { } seminar && state.SeminarId != seminar)
            return $"仅限研究会：{content.Seminar(seminar).Name}";
        if (requirements.RequiredCharacterId is { } characterId)
        {
            if (!state.Relationships.TryGetValue(characterId, out var relationship) || relationship.Stage == "stranger")
                return $"需要认识角色：{content.Character(characterId).Name}";
            if (requirements.RequiredRelationshipStage is { } stage && StageRank(relationship.Stage) < StageRank(stage))
                return $"需要与{relationship.Name}达到{StageName(stage)}关系";
        }
        return null;
    }

    public IReadOnlyList<CardUnlockConditionView> GetUnlockConditions(CardDefinition card, GameState state)
    {
        var requirements = card.UnlockRequirements;
        var conditions = new List<CardUnlockConditionView>();
        foreach (var (category, requiredLevel) in requirements.RequiredCoreAttributeLevels)
        {
            var currentLevel = state.Core.Level(category);
            conditions.Add(new()
            {
                Type = "coreAttribute",
                Label = content.Label(category),
                Required = $"Lv{requiredLevel}",
                Current = $"Lv{currentLevel}",
                Satisfied = currentLevel >= requiredLevel
            });
        }
        foreach (var flag in requirements.RequiredFlags)
            conditions.Add(FlagCondition(flag, state.Flags.GetValueOrDefault(flag), true));
        foreach (var flag in requirements.ForbiddenFlags)
            conditions.Add(FlagCondition(flag, state.Flags.GetValueOrDefault(flag), false));
        if (requirements.RequiredFaculty is { } faculty)
            conditions.Add(new()
            {
                Type = "faculty",
                Label = "学部",
                Required = content.Faculty(faculty).Name,
                Current = content.Faculty(state.FacultyId).Name,
                Satisfied = state.FacultyId == faculty
            });
        if (requirements.RequiredSeminar is { } seminar)
            conditions.Add(new()
            {
                Type = "seminar",
                Label = "研究会",
                Required = content.Seminar(seminar).Name,
                Current = content.Seminar(state.SeminarId).Name,
                Satisfied = state.SeminarId == seminar
            });
        if (requirements.RequiredCharacterId is { } characterId)
        {
            var relationship = state.Relationships.GetValueOrDefault(characterId);
            conditions.Add(new()
            {
                Type = "character",
                Label = "认识角色",
                Required = content.Character(characterId).Name,
                Current = relationship is null || relationship.Stage == "stranger" ? "尚未认识" : "已认识",
                Satisfied = relationship is not null && relationship.Stage != "stranger"
            });
            if (requirements.RequiredRelationshipStage is { } stage)
                conditions.Add(new()
                {
                    Type = "relationship",
                    Label = $"与{content.Character(characterId).Name}的关系",
                    Required = StageName(stage),
                    Current = StageName(relationship?.Stage ?? "stranger"),
                    Satisfied = relationship is not null && StageRank(relationship.Stage) >= StageRank(stage)
                });
        }
        return conditions;
    }

    private static CardUnlockConditionView FlagCondition(string flag, bool currentValue, bool requiredValue) => new()
    {
        Type = "flag",
        Label = requiredValue ? "需要事件 / Flag" : "禁止事件 / Flag",
        Required = flag,
        Current = currentValue ? "已触发" : "未触发",
        Satisfied = currentValue == requiredValue
    };

    private static int StageRank(string stage) => stage switch
    {
        "acquaintance" => 1,
        "friend" => 2,
        "close" => 3,
        "special" => 4,
        _ => 0
    };

    private static string StageName(string stage) => stage switch
    {
        "acquaintance" => "相识",
        "friend" => "朋友",
        "close" => "亲近",
        "special" => "特别",
        _ => stage
    };
}
