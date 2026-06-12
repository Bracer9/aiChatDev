namespace NichiryuSim;

public sealed class GameState
{
    public int CurrentMonth { get; set; } = 1;
    public int MaxMonth { get; set; } = 12;
    public string CurrentUiState { get; set; } = "StartMenu";
    public string FacultyId { get; set; } = "media_culture";
    public string SeminarId { get; set; } = "magical_girl";
    public OpeningNarrationPayload? OpeningNarration { get; set; }
    public RelationshipInteractionPayload? RelationshipInteraction { get; set; }
    public PlayerStats Stats { get; set; } = new();
    public HousingState Housing { get; set; } = new();
    public MonthlyExpenseState MonthlyExpense { get; set; } = new();
    public TuitionState Tuition { get; set; } = new();
    public CoreAttributes Core { get; set; } = new();
    public int UnspentLifeExperiencePoints { get; set; }
    public int TotalLifeExperiencePointsEarned { get; set; }
    public int MonthlyDrawCount { get; set; } = 5;
    public int MonthlySelectLimit { get; set; } = 3;
    public int MonthlySwitchLimit { get; set; } = 2;
    public int EffectiveMonthlyDrawCount => MonthlyDrawCount + (Core.Level("life") >= 2 ? 1 : 0);
    public Dictionary<string, int> CardExecutionCounters { get; set; } = new();
    public Dictionary<string, bool> Flags { get; set; } = new();
    public Dictionary<string, CharacterRelationship> Relationships { get; set; } = new();
    public List<string> TriggeredEventIds { get; set; } = [];
    public List<string> CurrentMonthEventIds { get; set; } = [];
    public List<Opportunity> Opportunities { get; set; } = [];
    public string? SelectedOpportunityId { get; set; }
    public List<MonthlyOpportunityCard> CurrentMonthHand { get; set; } = [];
    public List<MonthlyOpportunityCard> SelectedMonthCards { get; set; } = [];
    public MonthlyOpportunityCard? ReservedCardForNextMonth { get; set; }
    public bool HasRefreshedCardsThisMonth { get; set; }
    public MonthResolution? LastResolution { get; set; }
    public AiPayloadBundle? StoredAiPayloads { get; set; }
    public List<string> MonthlyLogs { get; set; } = [];
    public int RelationshipActionsUsedThisMonth { get; set; }
    public int HospitalizedCount { get; set; }
    public int BurnoutCount { get; set; }
    public string? EndingId { get; set; }
}

public sealed class PlayerStats
{
    public int HP { get; set; } = 100;
    public int MaxHP { get; set; } = 100;
    public int MP { get; set; } = 100;
    public int MaxMP { get; set; } = 100;
    public int Money { get; set; } = 180000;
}

public sealed class HousingState
{
    public string HousingId { get; set; } = "standard_apartment";
    public string Name { get; set; } = "standard_apartment";
    public int Rent { get; set; } = 42000;
    public int HousingComfort { get; set; } = 55;
    public int CommuteBurden { get; set; } = 35;
}

public sealed class MonthlyExpenseState
{
    public int LivingCost { get; set; } = 36000;
    public int CommunicationCost { get; set; } = 5000;
    public int TransportationCost { get; set; } = 8000;
}

public sealed class TuitionState
{
    public int Amount { get; set; } = 320000;
    public int IntervalMonths { get; set; } = 6;
    public int NextDueMonth { get; set; } = 6;
}

public sealed class CoreAttributes
{
    public const int ExperiencePerLifeExperiencePoint = 10;
    public static readonly string[] Categories = ["academic", "language", "career", "creation", "life", "relationship"];

    public int Academic { get; set; }
    public int Language { get; set; }
    public int Career { get; set; }
    public int Creation { get; set; }
    public int Life { get; set; }
    public int Relationship { get; set; }
    public int Level(string category)
    {
        var experience = Get(category);
        var level = 0;
        while (experience >= TotalExperienceRequiredForLevel(level + 1)) level++;
        return level;
    }
    public int ExperienceIntoCurrentLevel(string category) => Get(category) - TotalExperienceRequiredForLevel(Level(category));
    public int ExperienceRequiredForNextLevel(string category) => TotalExperienceRequiredForLevel(Level(category) + 1) - Get(category);
    public int CurrentLevelExperienceRequirement(string category) =>
        TotalExperienceRequiredForLevel(Level(category) + 1) - TotalExperienceRequiredForLevel(Level(category));
    public static int TotalExperienceRequiredForLevel(int level) => 100 * level * (level + 1) / 2;
    public int Get(string category) => category switch
    {
        "academic" => Academic, "language" => Language, "career" => Career,
        "creation" => Creation, "life" => Life, "relationship" => Relationship, _ => 0
    };
    public void Add(string category, int value)
    {
        if (category == "academic") Academic += value;
        if (category == "language") Language += value;
        if (category == "career") Career += value;
        if (category == "creation") Creation += value;
        if (category == "life") Life += value;
        if (category == "relationship") Relationship += value;
    }
}

public sealed class CardDefinition
{
    public string CardId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string MeaningText { get; init; } = "";
    public string PrimaryCoreAttribute { get; init; } = "";
    public int CoreExpDelta { get; init; }
    public int HpDelta { get; init; }
    public int MpDelta { get; init; }
    public int MoneyDelta { get; init; }
    public string Rarity { get; init; } = "Common";
    public string CardType { get; init; } = "Action";
    public CardUnlockRequirements UnlockRequirements { get; init; } = new();
    public string[] CardTags { get; init; } = [];
    public HousingDelta HousingDelta { get; init; } = new();
    public int TuitionDelayMonths { get; init; }
    public string? RelatedCharacterId { get; init; }
    public string[] PossibleEventIds { get; init; } = [];
    public bool IsInitialCard { get; init; }
    public string[] InitialFacultyIds { get; init; } = [];
    public bool IsHiddenUntilUnlocked { get; init; }
    public bool CannotRepeatConsecutively { get; init; }
}

public sealed class MonthlyOpportunityCard
{
    public string CardId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string MeaningText { get; set; } = "";
    public string CardType { get; set; } = "";
    public string PrimaryCoreAttribute { get; set; } = "";
    public int CoreExpDelta { get; set; }
    public int HpDelta { get; set; }
    public int MpDelta { get; set; }
    public int MoneyDelta { get; set; }
    public string Rarity { get; set; } = "Common";
    public CardUnlockRequirements UnlockRequirements { get; set; } = new();
    public List<string> CardTags { get; set; } = [];
    public HousingDelta HousingDelta { get; set; } = new();
    public int TuitionDelayMonths { get; set; }
    public List<string> PossibleEventIds { get; set; } = [];
    public string? LockedReason { get; set; }
    public string? RelatedCharacterId { get; set; }
    public bool IsInitialCard { get; set; }
    public List<string> InitialFacultyIds { get; set; } = [];
    public bool IsHiddenUntilUnlocked { get; set; }
    public bool CannotRepeatConsecutively { get; set; }
    public bool IsLimited { get; set; }
    public int? ExpireMonth { get; set; }
    public bool IsPinnedFromLastMonth { get; set; }
    public bool IsSelected { get; set; }
    public bool IsReservedForNextMonth { get; set; }
    public string? CustomNote { get; set; }
}

public sealed class CardUnlockRequirements
{
    public Dictionary<string, int> RequiredCoreAttributeLevels { get; set; } = [];
    public List<string> RequiredFlags { get; set; } = [];
    public List<string> ForbiddenFlags { get; set; } = [];
    public string? RequiredFaculty { get; set; }
    public string? RequiredSeminar { get; set; }
    public string? RequiredCharacterId { get; set; }
    public string? RequiredRelationshipStage { get; set; }
}

public sealed class CardUnlockConditionView
{
    public string Type { get; set; } = "";
    public string Label { get; set; } = "";
    public string Required { get; set; } = "";
    public string Current { get; set; } = "";
    public bool Satisfied { get; set; }
}

public sealed class HousingDelta
{
    public int RentDelta { get; set; }
    public int HousingComfortDelta { get; set; }
    public int CommuteBurdenDelta { get; set; }
}

public sealed class CharacterRelationship
{
    public string CharacterId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Stage { get; set; } = "stranger";
    public int Affection { get; set; }
    public int Trust { get; set; }
    public string Mood { get; set; } = "neutral";
    public int MoodValue { get; set; }
    public int InteractionCount { get; set; }
    public int? LastInteractionMonth { get; set; }
    public string? LastActionId { get; set; }
    public List<CharacterMemory> Memories { get; set; } = [];
}

public sealed class CharacterMemory
{
    public string Id { get; set; } = "";
    public int Month { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public int Importance { get; set; } = 1;
    public string? ActionId { get; set; }
    public string? EventId { get; set; }
    public List<string> Tags { get; set; } = [];
}

public sealed class ActionResult
{
    public string ActionName { get; set; } = "";
    public string Result { get; set; } = "";
    public int Score { get; set; }
    public string Detail { get; set; } = "";
}

public sealed class MonthResolution
{
    public int Month { get; set; }
    public int HpDelta { get; set; }
    public int MpDelta { get; set; }
    public int MoneyDelta { get; set; }
    public int ActionMoneyDelta { get; set; }
    public int FixedExpenseTotal { get; set; }
    public int TuitionPaid { get; set; }
    public int MoneyAfterActions { get; set; }
    public int MoneyAfterExpenses { get; set; }
    public string FinancialRisk { get; set; } = "";
    public int LifeExperiencePointsEarned { get; set; }
    public Dictionary<string, int> LifeExperienceAllocations { get; set; } = [];
    public Dictionary<string, int> CoreDelta { get; set; } = [];
    public List<ActionResult> Actions { get; set; } = [];
    public List<string> Events { get; set; } = [];
}

public sealed class Opportunity
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Risk { get; set; } = "";
    public string Reward { get; set; } = "";
    public bool Selected { get; set; }
}

public sealed class EventDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public int StartMonth { get; init; } = 1;
    public int EndMonth { get; init; } = 12;
    public List<string> RequiredSeminarIds { get; init; } = [];
    public List<string> RelatedCharacterIds { get; init; } = [];
    public List<string> RequiredFlags { get; init; } = [];
    public List<string> SetFlags { get; init; } = [];
}

public sealed class FacultyDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Theme { get; set; } = "";
    public string DefaultSeminarId { get; set; } = "";
    public Dictionary<string, int> InitialCoreDelta { get; set; } = [];
    public Dictionary<string, int> EventMonths { get; set; } = [];
}

public sealed class SeminarDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Theme { get; set; } = "";
    public List<string> FacultyIds { get; set; } = [];
    public List<string> InitialCharacterIds { get; set; } = [];
    public List<string> StartEventIds { get; set; } = [];
    public List<string> SetFlags { get; set; } = [];
    public List<string> OpportunityBias { get; set; } = [];
}

public sealed class OpeningNarrationPayload
{
    public string Source { get; set; } = "pending";
    public string? FallbackReason { get; set; }
    public string FacultyId { get; set; } = "";
    public string Title { get; set; } = "";
    public List<string> Paragraphs { get; set; } = [];
    public AiUsageSnapshot? Usage { get; set; }
}

public sealed class RelationshipInteractionPayload
{
    public string Type { get; set; } = "visual_novel_scene";
    public string Source { get; set; } = "pending";
    public string? FallbackReason { get; set; }
    public string InteractionId { get; set; } = "";
    public string SceneId { get; set; } = "";
    public string BackgroundId { get; set; } = "";
    public string BackgroundPath { get; set; } = "";
    public string CharacterId { get; set; } = "";
    public string ActionId { get; set; } = "";
    public string Title { get; set; } = "";
    public string SceneText { get; set; } = "";
    public string Mood { get; set; } = "";
    public List<VisualNovelCharacter> Characters { get; set; } = [];
    public List<VisualNovelLine> Lines { get; set; } = [];
    public List<VisualNovelInteractionOption> InteractionOptions { get; set; } = [];
    public string? SelectedOptionId { get; set; }
    public int ChoiceAffectionDelta { get; set; }
    public string ChoiceResultText { get; set; } = "";
    public string ResultText { get; set; } = "";
    public string MemoryUpdate { get; set; } = "";
    public int AffectionDelta { get; set; }
    public int TrustDelta { get; set; }
    public string StageBefore { get; set; } = "";
    public string StageAfter { get; set; } = "";
    public AiUsageSnapshot? Usage { get; set; }
}

public sealed class VisualNovelCharacter
{
    public string CharacterId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PortraitId { get; set; } = "";
    public string PortraitPath { get; set; } = "";
}

public sealed class VisualNovelLine
{
    public string LineType { get; set; } = "narration";
    public string SpeakerId { get; set; } = "narrator";
    public string SpeakerName { get; set; } = "旁白";
    public string Text { get; set; } = "";
    public string? BackgroundId { get; set; }
    public string? BackgroundPath { get; set; }
    public string? PortraitId { get; set; }
    public string? PortraitPath { get; set; }
    public string Expression { get; set; } = "neutral";
}

public sealed class VisualNovelInteractionOption
{
    public string OptionId { get; set; } = "";
    public string Text { get; set; } = "";
    public int AffectionDelta { get; set; }
    public string ResultText { get; set; } = "";
}

public sealed class AiPayloadBundle
{
    public string Type { get; set; } = "monthly_ai_payload_bundle";
    public int Month { get; set; }
    public string Source { get; set; } = "mock";
    public string? FallbackReason { get; set; }
    public AiUsageSnapshot? Usage { get; set; }
    public MonthlyReviewPayload? MonthlyReviewPayload { get; set; }
    public List<EventScenePayload> EventScenePayloads { get; set; } = [];
    public List<RelationshipPayload> RelationshipPayloads { get; set; } = [];
    public List<OpportunityPayload> OpportunityPayloads { get; set; } = [];
    public ArchiveMemoryPayload? ArchiveMemoryPayload { get; set; }

    // Compatibility fields consumed by the current UI.
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<string> Paragraphs { get; set; } = [];
    public Dictionary<string, string> RelationshipTexts { get; set; } = [];
    public List<string> EventScenes { get; set; } = [];
}

public sealed class AiUsageSnapshot
{
    public string Model { get; set; } = "";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public int PromptCacheHitTokens { get; set; }
    public int PromptCacheMissTokens { get; set; }
}

public sealed class MonthlyReviewPayload
{
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<string> Paragraphs { get; set; } = [];
}

public sealed class EventScenePayload
{
    public string EventId { get; set; } = "";
    public string Title { get; set; } = "";
    public string SceneText { get; set; } = "";
}

public sealed class RelationshipPayload
{
    public string CharacterId { get; set; } = "";
    public string StatusText { get; set; } = "";
}

public sealed class OpportunityPayload
{
    public string OpportunityId { get; set; } = "";
    public string FlavorText { get; set; } = "";
}

public sealed class ArchiveMemoryPayload
{
    public string LogTitle { get; set; } = "";
    public string LogText { get; set; } = "";
}

public sealed class AiNarrationOptions
{
    public string Mode { get; set; } = "Mock";
    public string Endpoint { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string Model { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 45;
}

public sealed class AiSettingsRequest
{
    public string Mode { get; set; } = "Mock";
    public string Endpoint { get; set; } = "https://api.deepseek.com/chat/completions";
    public string Model { get; set; } = "deepseek-v4-flash";
    public string ApiKey { get; set; } = "";
    public bool ClearApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 45;
}

public sealed class AiSettingsView
{
    public string Mode { get; set; } = "Mock";
    public string Endpoint { get; set; } = "";
    public string Model { get; set; } = "";
    public bool HasApiKey { get; set; }
    public string MaskedApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; }
    public List<AiModelOption> ModelOptions { get; set; } = [];
}

public sealed class AiModelOption
{
    public string Label { get; set; } = "";
    public string Model { get; set; } = "";
    public string Description { get; set; } = "";
}

public record NewGameRequest(string? FacultyId, string? SeminarId);
public sealed class MonthPlanRequest
{
    public List<string> SelectedCardIds { get; set; } = [];
    public List<PlanCardSelection> SelectedCards { get; set; } = [];
}

public sealed class PlanCardSelection
{
    public string CardId { get; set; } = "";
    public string? FreeCategory { get; set; }
    public string CustomNote { get; set; } = "";
}
public record OpportunityRequest(string OpportunityId);
public record CardRequest(string CardId);
public record RefreshCardsRequest(List<string> CardIds);
public record RelationshipActionRequest(string CharacterId, string RelationshipActionId, string? SceneId = null);
public record RelationshipChoiceRequest(string InteractionId, string OptionId);
public sealed class CoreAttributeAllocationRequest
{
    public Dictionary<string, int> Allocations { get; set; } = [];
}
public record SaveSlotRequest(int Slot);

public sealed class SaveFile
{
    public int Version { get; set; } = 1;
    public int Slot { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public GameState State { get; set; } = new();
}

public sealed class SaveSlotInfo
{
    public int Slot { get; set; }
    public bool Exists { get; set; }
    public DateTimeOffset? SavedAt { get; set; }
    public int? CurrentMonth { get; set; }
    public int? Money { get; set; }
    public string? UiState { get; set; }
}
