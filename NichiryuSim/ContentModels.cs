using System.Text.Json;

namespace NichiryuSim;

public sealed class ContentService
{
    private readonly GameContent _content;
    private readonly string _contentRoot;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ContentService(IWebHostEnvironment environment)
    {
        _contentRoot = Path.Combine(environment.ContentRootPath, "Content", "zh-CN");
        _content = new()
        {
            Labels = Load<Dictionary<string, string>>("labels.json"),
            Faculties = Load<List<FacultyDefinition>>("faculties.json"),
            Seminars = Load<List<SeminarDefinition>>("seminars.json"),
            Events = Load<List<EventDefinition>>("events.json"),
            Cards = Load<List<CardContent>>("cards.json"),
            Characters = Load<List<CharacterContent>>("characters.json"),
            Scenes = Load<List<SceneContent>>("scenes.json"),
            Opportunities = Load<List<OpportunityContent>>("opportunities.json"),
            Messages = Load<Dictionary<string, string>>("messages.json")
        };
        Validate();
    }

    public IReadOnlyDictionary<string, string> Labels => _content.Labels;
    public IReadOnlyList<FacultyDefinition> Faculties => _content.Faculties;
    public IReadOnlyList<SeminarDefinition> Seminars => _content.Seminars;
    public IReadOnlyList<EventDefinition> Events => _content.Events;
    public IReadOnlyList<CharacterContent> Characters => _content.Characters;
    public IReadOnlyList<SceneContent> Scenes => _content.Scenes;

    public IReadOnlyList<CardDefinition> CreateCards() =>
        _content.Cards.Select(card => new CardDefinition
        {
            CardId = card.CardId,
            Name = card.Name,
            Description = card.Description,
            MeaningText = card.MeaningText,
            PrimaryCoreAttribute = card.PrimaryCoreAttribute,
            CoreExpDelta = card.CoreExpDelta,
            HpDelta = card.HpDelta,
            MpDelta = card.MpDelta,
            MoneyDelta = card.MoneyDelta,
            Rarity = card.Rarity,
            CardType = card.CardType,
            UnlockRequirements = card.UnlockRequirements,
            CardTags = card.CardTags.ToArray(),
            HousingDelta = card.HousingDelta,
            TuitionDelayMonths = card.TuitionDelayMonths,
            RelatedCharacterId = card.RelatedCharacterId,
            PossibleEventIds = card.PossibleEventIds.ToArray(),
            IsInitialCard = card.IsInitialCard,
            InitialFacultyIds = card.InitialFacultyIds.ToArray(),
            IsHiddenUntilUnlocked = HiddenUntilUnlocked(card),
            CannotRepeatConsecutively = card.CannotRepeatConsecutively
        }).ToList();

    public string Label(string category) => _content.Labels.GetValueOrDefault(category, category);
    public FacultyDefinition Faculty(string id) =>
        _content.Faculties.FirstOrDefault(x => x.Id == id) ?? _content.Faculties.First();
    public SeminarDefinition Seminar(string id) =>
        _content.Seminars.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException($"Missing seminar content: {id}");
    public CharacterContent Character(string id) =>
        _content.Characters.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException($"Missing character content: {id}");
    public SceneContent Scene(string id) =>
        _content.Scenes.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException($"Missing scene content: {id}");
    public SceneContent DefaultScene() =>
        _content.Scenes.FirstOrDefault(x => x.IsDefault) ?? _content.Scenes.First();
    public OpportunityContent Opportunity(string id) =>
        _content.Opportunities.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException($"Missing opportunity content: {id}");
    public string Message(string key) => _content.Messages.GetValueOrDefault(key, key);
    public string Format(string key, params object?[] args) => string.Format(Message(key), args);

    private T Load<T>(params string[] pathParts)
    {
        var path = Path.Combine([_contentRoot, .. pathParts]);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Content file not found: {path}");

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), _jsonOptions)
            ?? throw new InvalidOperationException($"Content file is empty or invalid: {path}");
    }

    private void Validate()
    {
        EnsureUnique(_content.Events.Select(x => x.Id), "event");
        EnsureUnique(_content.Faculties.Select(x => x.Id), "faculty");
        EnsureUnique(_content.Seminars.Select(x => x.Id), "seminar");
        EnsureUnique(_content.Cards.Select(x => x.CardId), "card");
        EnsureUnique(_content.Characters.Select(x => x.Id), "character");
        EnsureUnique(_content.Scenes.Select(x => x.Id), "scene");

        var facultyIds = _content.Faculties.Select(x => x.Id).ToHashSet();
        var seminarIds = _content.Seminars.Select(x => x.Id).ToHashSet();
        var characterIds = _content.Characters.Select(x => x.Id).ToHashSet();
        var eventIds = _content.Events.Select(x => x.Id).ToHashSet();
        var sceneIds = _content.Scenes.Select(x => x.Id).ToHashSet();
        var coreCategories = new HashSet<string> { "academic", "language", "career", "creation", "life", "relationship" };
        foreach (var card in _content.Cards)
        {
            if (string.IsNullOrWhiteSpace(card.Description) || string.IsNullOrWhiteSpace(card.MeaningText))
                throw new InvalidOperationException($"Card '{card.CardId}' must define description and meaning text.");
            if (!coreCategories.Contains(card.PrimaryCoreAttribute))
                throw new InvalidOperationException($"Card '{card.CardId}' has invalid primary core attribute '{card.PrimaryCoreAttribute}'.");
            if (card.CoreExpDelta <= 0)
                throw new InvalidOperationException($"Card '{card.CardId}' must grant positive core attribute experience.");
            if (card.Rarity is not ("Common" or "Rare" or "Special"))
                throw new InvalidOperationException($"Card '{card.CardId}' has invalid rarity '{card.Rarity}'.");
            if (card.IsInitialCard && card.Rarity != "Common")
                throw new InvalidOperationException($"Initial card '{card.CardId}' must be Common.");
            if (card.IsInitialCard && HiddenUntilUnlocked(card))
                throw new InvalidOperationException($"Initial card '{card.CardId}' cannot be hidden until unlocked.");
            foreach (var initialFacultyId in card.InitialFacultyIds)
                if (!facultyIds.Contains(initialFacultyId)) throw new InvalidOperationException($"Card '{card.CardId}' references missing initial faculty '{initialFacultyId}'.");
            if (card.InitialFacultyIds.Count > 0 && card.Rarity != "Common")
                throw new InvalidOperationException($"Faculty initial card '{card.CardId}' must be Common.");
            if (card.IsInitialCard && card.InitialFacultyIds.Count > 0)
                throw new InvalidOperationException($"Card '{card.CardId}' cannot be both a default and faculty initial card.");
            if (card.InitialFacultyIds.Count > 0 && !HasUnlockRequirements(card.UnlockRequirements))
                throw new InvalidOperationException($"Faculty initial card '{card.CardId}' must define unlock requirements for other faculties.");
            foreach (var category in card.UnlockRequirements.RequiredCoreAttributeLevels.Keys)
                if (!coreCategories.Contains(category)) throw new InvalidOperationException($"Card '{card.CardId}' references invalid core attribute '{category}'.");
            if (card.UnlockRequirements.RequiredFaculty is { } facultyId && !facultyIds.Contains(facultyId))
                throw new InvalidOperationException($"Card '{card.CardId}' references missing faculty '{facultyId}'.");
            if (card.UnlockRequirements.RequiredSeminar is { } seminarId && !seminarIds.Contains(seminarId))
                throw new InvalidOperationException($"Card '{card.CardId}' references missing seminar '{seminarId}'.");
            if (card.UnlockRequirements.RequiredCharacterId is { } characterId && !characterIds.Contains(characterId))
                throw new InvalidOperationException($"Card '{card.CardId}' references missing character '{characterId}'.");
            if (card.RelatedCharacterId is { } relatedCharacterId && !characterIds.Contains(relatedCharacterId))
                throw new InvalidOperationException($"Card '{card.CardId}' references missing related character '{relatedCharacterId}'.");
            foreach (var eventId in card.PossibleEventIds)
                if (!eventIds.Contains(eventId)) throw new InvalidOperationException($"Card '{card.CardId}' references missing event '{eventId}'.");
            if (card.UnlockRequirements.RequiredRelationshipStage is { } stage && stage is not ("acquaintance" or "friend" or "close" or "special"))
                throw new InvalidOperationException($"Card '{card.CardId}' has invalid relationship stage '{stage}'.");
        }
        foreach (var character in _content.Characters)
        {
            if (!sceneIds.Contains(character.DefaultSceneId))
                throw new InvalidOperationException($"Character '{character.Id}' references missing default scene '{character.DefaultSceneId}'.");
            foreach (var seminarId in character.AvailableSeminarIds)
                if (!seminarIds.Contains(seminarId)) throw new InvalidOperationException($"Character '{character.Id}' references missing seminar '{seminarId}'.");
            foreach (var (actionId, preference) in character.InteractionPreferences)
                foreach (var category in preference.CoreDelta.Keys)
                    if (!coreCategories.Contains(category)) throw new InvalidOperationException($"Character '{character.Id}' interaction '{actionId}' references invalid core category '{category}'.");
        }
        foreach (var seminar in _content.Seminars)
            foreach (var characterId in seminar.InitialCharacterIds)
                if (!characterIds.Contains(characterId)) throw new InvalidOperationException($"Seminar '{seminar.Id}' references missing character '{characterId}'.");
        foreach (var definition in _content.Events)
            foreach (var characterId in definition.RelatedCharacterIds)
                if (!characterIds.Contains(characterId)) throw new InvalidOperationException($"Event '{definition.Id}' references missing character '{characterId}'.");

        if (_content.Scenes.Count == 0 || _content.Scenes.All(x => !x.IsDefault))
            throw new InvalidOperationException("Scene content must define at least one default scene.");
        foreach (var scene in _content.Scenes)
            if (string.IsNullOrWhiteSpace(scene.Name) || string.IsNullOrWhiteSpace(scene.Description) || string.IsNullOrWhiteSpace(scene.BackgroundPath))
                throw new InvalidOperationException($"Scene '{scene.Id}' must define name, description, and background path.");

        if (_content.Cards.Count(x => x.IsInitialCard) != 6)
            throw new InvalidOperationException("The default initial deck must contain exactly 6 cards.");
        foreach (var faculty in _content.Faculties)
            if (_content.Cards.Count(x => x.InitialFacultyIds.Contains(faculty.Id)) != 4)
                throw new InvalidOperationException($"Faculty '{faculty.Id}' must define exactly 4 faculty initial cards.");
    }

    private static void EnsureUnique(IEnumerable<string> ids, string kind)
    {
        var duplicate = ids.GroupBy(x => x).FirstOrDefault(x => string.IsNullOrWhiteSpace(x.Key) || x.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Invalid or duplicated {kind} id: '{duplicate.Key}'.");
    }

    private static bool HiddenUntilUnlocked(CardContent card) =>
        card.IsHiddenUntilUnlocked ?? card.Rarity == "Special";

    private static bool HasUnlockRequirements(CardUnlockRequirements requirements) =>
        requirements.RequiredCoreAttributeLevels.Count > 0
        || requirements.RequiredFlags.Count > 0
        || requirements.ForbiddenFlags.Count > 0
        || requirements.RequiredFaculty is not null
        || requirements.RequiredSeminar is not null
        || requirements.RequiredCharacterId is not null
        || requirements.RequiredRelationshipStage is not null;
}

public sealed class GameContent
{
    public Dictionary<string, string> Labels { get; set; } = [];
    public List<FacultyDefinition> Faculties { get; set; } = [];
    public List<SeminarDefinition> Seminars { get; set; } = [];
    public List<EventDefinition> Events { get; set; } = [];
    public List<CardContent> Cards { get; set; } = [];
    public List<CharacterContent> Characters { get; set; } = [];
    public List<SceneContent> Scenes { get; set; } = [];
    public List<OpportunityContent> Opportunities { get; set; } = [];
    public Dictionary<string, string> Messages { get; set; } = [];
}

public sealed class CardContent
{
    public string CardId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string MeaningText { get; set; } = "";
    public string PrimaryCoreAttribute { get; set; } = "";
    public int CoreExpDelta { get; set; }
    public int HpDelta { get; set; }
    public int MpDelta { get; set; }
    public int MoneyDelta { get; set; }
    public string Rarity { get; set; } = "Common";
    public string CardType { get; set; } = "Action";
    public CardUnlockRequirements UnlockRequirements { get; set; } = new();
    public List<string> CardTags { get; set; } = [];
    public HousingDelta HousingDelta { get; set; } = new();
    public int TuitionDelayMonths { get; set; }
    public string? RelatedCharacterId { get; set; }
    public List<string> PossibleEventIds { get; set; } = [];
    public bool IsInitialCard { get; set; }
    public List<string> InitialFacultyIds { get; set; } = [];
    public bool? IsHiddenUntilUnlocked { get; set; }
    public bool CannotRepeatConsecutively { get; set; }
}

public sealed class CharacterContent
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Gender { get; set; } = "";
    public int Age { get; set; }
    public int Year { get; set; }
    public string Role { get; set; } = "";
    public string Background { get; set; } = "";
    public string CurrentGoal { get; set; } = "";
    public string Stage { get; set; } = "stranger";
    public int Affection { get; set; }
    public int Trust { get; set; }
    public string InitialMood { get; set; } = "neutral";
    public int InitialMoodValue { get; set; }
    public List<string> AvailableSeminarIds { get; set; } = [];
    public List<string> PersonalityTags { get; set; } = [];
    public List<string> Values { get; set; } = [];
    public List<string> Likes { get; set; } = [];
    public List<string> Dislikes { get; set; } = [];
    public List<string> Boundaries { get; set; } = [];
    public string SpeechStyle { get; set; } = "";
    public string DefaultPortraitId { get; set; } = "default_npc";
    public string DefaultPortraitPath { get; set; } = "/assets/portraits/default_npc.png";
    public string DefaultSceneId { get; set; } = "classroom_daytime";
    public Dictionary<string, CharacterPortraitContent> Portraits { get; set; } = [];
    public Dictionary<string, CharacterInteractionPreference> InteractionPreferences { get; set; } = [];
    public Dictionary<string, List<string>> StageSetFlags { get; set; } = [];
}

public sealed class CharacterPortraitContent
{
    public string PortraitId { get; set; } = "";
    public string PortraitPath { get; set; } = "";
}

public sealed class SceneContent
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string BackgroundPath { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    public bool IsDefault { get; set; }
}

public sealed class CharacterInteractionPreference
{
    public double AffectionMultiplier { get; set; } = 1;
    public double TrustMultiplier { get; set; } = 1;
    public int MoodDelta { get; set; }
    public int MemoryImportance { get; set; } = 1;
    public List<string> MemoryTags { get; set; } = [];
    public Dictionary<string, int> CoreDelta { get; set; } = [];
    public List<string> SetFlags { get; set; } = [];
}

public sealed class OpportunityContent
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Risk { get; set; } = "";
    public string Reward { get; set; } = "";
}
