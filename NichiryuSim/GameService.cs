using System.Text.Json;

namespace NichiryuSim;

public sealed class GameService
{
    private readonly Random _random = new();
    private static readonly JsonSerializerOptions CloneOptions = new(JsonSerializerDefaults.Web);
    private readonly EventService _events;
    private readonly CardService _cards;
    private readonly MonthlyDeckService _monthlyDeck;
    private readonly ContentService _content;
    private readonly SaveService _saves;
    private readonly IAiNarrationService _ai;
    private readonly CharacterService _characters;
    private readonly VisualNovelSceneService _visualNovelScenes;
    private readonly object _gate = new();
    private GameState _state;
    public IReadOnlyList<CardDefinition> Cards { get; }

    public GameService(EventService events, CardService cards, MonthlyDeckService monthlyDeck, ContentService content, SaveService saves, IAiNarrationService ai, CharacterService characters, VisualNovelSceneService visualNovelScenes)
    {
        _events = events;
        _cards = cards;
        _monthlyDeck = monthlyDeck;
        _content = content;
        _saves = saves;
        _ai = ai;
        _characters = characters;
        _visualNovelScenes = visualNovelScenes;
        Cards = content.CreateCards();
        _state = CreateInitialState(content);
    }

    public object GetView() => new { state = _state, cards = Cards, cardCatalog = GetCardCatalog(), labels = _content.Labels, faculties = _content.Faculties, seminars = _content.Seminars, characters = _content.Characters, finance = BuildFinanceView(), relationship = BuildRelationshipView(), saves = _saves.ListSlots() };

    public object GetCardCatalog() => Cards.Select(card =>
    {
        var lockedReason = _cards.GetUnlockReason(card, _state);
        return new { card, unlocked = lockedReason is null, lockedReason, unlockConditions = _cards.GetUnlockConditions(card, _state) };
    });

    public object NewGame(NewGameRequest request)
    {
        var faculty = _content.Faculty(request.FacultyId ?? "media_culture");
        var seminar = _content.Seminar(request.SeminarId ?? faculty.DefaultSeminarId);
        if (!seminar.FacultyIds.Contains(faculty.Id))
            return Error("该 seminar 不属于所选学部。");
        _state = CreateInitialState(_content);
        _state.CurrentUiState = "Opening";
        _state.FacultyId = faculty.Id;
        _state.SeminarId = seminar.Id;
        _state.UnspentLifeExperiencePoints = 6;
        _state.TotalLifeExperiencePointsEarned = 6;
        foreach (var (category, value) in faculty.InitialCoreDelta)
            _state.Core.Add(category, value);
        foreach (var characterId in seminar.InitialCharacterIds)
            _characters.EnsureRelationship(_state, characterId);
        foreach (var flag in seminar.SetFlags) _state.Flags[flag] = true;
        foreach (var eventId in seminar.StartEventIds) _events.Trigger(_state, eventId);
        _state.OpeningNarration = CreateLocalOpening(faculty, seminar);
        return GetView();
    }

    public object BeginFirstMonth()
    {
        _monthlyDeck.GenerateForMonth(_state, Cards);
        _state.CurrentUiState = "MonthStart";
        return GetView();
    }

    public object ResolveMonth(MonthPlanRequest request)
    {
        if (_state.CurrentUiState != "MonthPlan") return Error(_content.Message("error.month.already_resolved"));
        List<MonthlyOpportunityCard> selected;
        try
        {
            selected = ResolveSelectedCards(request);
        }
        catch (ArgumentException ex)
        {
            return Error(ex.Message);
        }
        if (selected.Count < 1 || selected.Count > _state.MonthlySelectLimit)
            return Error(_content.Format("error.month.selection_count", _state.MonthlySelectLimit));

        var beforeHp = _state.Stats.HP;
        var beforeMp = _state.Stats.MP;
        var beforeMoney = _state.Stats.Money;
        var beforeCore = CoreSnapshot();
        var resolution = new MonthResolution { Month = _state.CurrentMonth };
        string? previousCard = null;

        foreach (var card in selected)
        {
            if (_state.Flags.GetValueOrDefault("burnout"))
            {
                resolution.Actions.Add(new() { ActionName = card.Name, Result = _content.Message("result.cancelled"), Detail = _content.Message("detail.burnout_rest_only") });
                continue;
            }

            if (card.CannotRepeatConsecutively && previousCard == card.CardId)
            {
                resolution.Actions.Add(new() { ActionName = card.Name, Result = _content.Message("result.skipped"), Detail = _content.Message("detail.no_consecutive") });
                continue;
            }

            var score = Score(card);
            var gradeKey = score < 40 ? "result.fail" : score >= 75 ? "result.great_success" : "result.normal";
            var multiplier = gradeKey == "result.fail" ? 0.5 : gradeKey == "result.great_success" ? 1.5 : 1.0;
            var lifeExperiencePoints = gradeKey == "result.great_success" ? 2 : 1;
            var hpDelta = card.HpDelta;
            var mpDelta = card.MpDelta - (gradeKey == "result.fail" ? 3 : 0);
            _state.Stats.HP = Math.Min(100, _state.Stats.HP + hpDelta);
            _state.Stats.MP = Math.Min(100, _state.Stats.MP + mpDelta);
            _state.Stats.Money += card.MoneyDelta;
            _state.Core.Add(card.PrimaryCoreAttribute, (int)Math.Round(card.CoreExpDelta * multiplier));
            ApplyHousingDelta(card.HousingDelta);
            if (card.TuitionDelayMonths > 0 && MonthsUntilTuition(_state) <= 2)
                _state.Tuition.NextDueMonth += card.TuitionDelayMonths;
            _state.CardExecutionCounters[card.CardId] = _state.CardExecutionCounters.GetValueOrDefault(card.CardId) + 1;
            resolution.Actions.Add(new() { ActionName = card.Name, Result = _content.Message(gradeKey), Score = score, Detail = card.CustomNote ?? "" });
            resolution.LifeExperiencePointsEarned += lifeExperiencePoints;
            previousCard = card.CardId;

            if (_state.Stats.HP <= 0)
            {
                _state.HospitalizedCount++;
                _state.Flags["hospitalized"] = true;
                _state.Stats.Money -= 10000;
                _state.Stats.HP = 50;
                _state.Stats.MP = Math.Max(0, _state.Stats.MP - 10);
                _state.CurrentMonthEventIds.Add("hospitalized");
                AddEventLog("hospitalized");
                break;
            }
            if (_state.Stats.MP <= 0)
            {
                _state.BurnoutCount++;
                _state.Flags["burnout"] = true;
                _state.Stats.MP = 50;
                _state.Stats.HP = Math.Min(100, _state.Stats.HP + 10);
                _state.CurrentMonthEventIds.Add("burnout");
                AddEventLog("burnout");
                break;
            }
        }

        _state.SelectedMonthCards = selected;
        resolution.MoneyAfterActions = _state.Stats.Money;
        resolution.ActionMoneyDelta = _state.Stats.Money - beforeMoney;
        ApplyMonthlyExpenses(resolution);
        _events.CheckMonthlyEvents(_state);
        resolution.Events = _state.CurrentMonthEventIds.ToList();
        GenerateOpportunities();
        resolution.HpDelta = _state.Stats.HP - beforeHp;
        resolution.MpDelta = _state.Stats.MP - beforeMp;
        resolution.MoneyDelta = _state.Stats.Money - beforeMoney;
        resolution.MoneyAfterExpenses = _state.Stats.Money;
        resolution.FinancialRisk = FinancialRiskLevel(_state.Stats.Money);
        resolution.CoreDelta = CoreSnapshot().ToDictionary(x => x.Key, x => x.Value - beforeCore[x.Key]);
        _state.LastResolution = resolution;
        _state.UnspentLifeExperiencePoints += resolution.LifeExperiencePointsEarned;
        _state.TotalLifeExperiencePointsEarned += resolution.LifeExperiencePointsEarned;
        _state.StoredAiPayloads = CreatePendingAiPayload(_state, resolution);
        var pendingLog = _content.Format("log.month_summary", _state.CurrentMonth, _state.StoredAiPayloads.Summary);
        _state.MonthlyLogs.Add(pendingLog);
        StartAiGeneration(DeepClone(_state), DeepClone(resolution), pendingLog);
        _state.CurrentUiState = "CoreAttributeAllocation";
        return GetView();
    }

    public object AllocateCoreAttributes(CoreAttributeAllocationRequest request)
    {
        if (_state.CurrentUiState != "CoreAttributeAllocation")
            return Error("当前不能分配人生经验点数。");

        if (request.Allocations.Any(x => !CoreAttributes.Categories.Contains(x.Key) || x.Value < 0))
            return Error("核心属性分配包含无效项目。");

        var spent = request.Allocations.Values.Sum();
        if (spent > _state.UnspentLifeExperiencePoints)
            return Error("未分配的人生经验点数不足。");

        foreach (var (category, points) in request.Allocations.Where(x => x.Value > 0))
        {
            _state.Core.Add(category, points * CoreAttributes.ExperiencePerLifeExperiencePoint);
            if (_state.LastResolution is not null)
                _state.LastResolution.LifeExperienceAllocations[category] =
                    _state.LastResolution.LifeExperienceAllocations.GetValueOrDefault(category) + points;
        }

        _state.UnspentLifeExperiencePoints -= spent;
        _state.CurrentUiState = "MonthResolution";
        return GetView();
    }

    public object SelectOpportunity(OpportunityRequest request)
    {
        if (_state.SelectedOpportunityId is not null) return Error(_content.Message("error.opportunity.decided"));
        var opportunity = _state.Opportunities.FirstOrDefault(x => x.Id == request.OpportunityId);
        if (opportunity is null) return Error(_content.Message("error.opportunity.missing"));
        opportunity.Selected = true;
        _state.SelectedOpportunityId = opportunity.Id;
        _state.Flags[opportunity.Id] = true;
        if (opportunity.Id == "workshop")
        {
            if (!_state.Relationships.TryGetValue("seminar_girl", out var rel))
                return Error(_content.Message("error.relationship.unavailable"));
            rel.Affection += 8; rel.Trust += 6;
            _characters.UpdateStageAndFlags(_state, _content.Character("seminar_girl"), rel);
            var hadEvent = HasEvent("workshop_team");
            _events.Trigger(_state, "workshop_team");
            if (!hadEvent)
            {
                _state.LastResolution?.Events.Add("workshop_team");
                _state.StoredAiPayloads?.EventScenes.Add(_content.Format("ai.event_scene", _events.Name("workshop_team")));
            }
        }
        _state.CurrentUiState = "Relationship";
        return GetView();
    }

    public object SkipOpportunity()
    {
        if (_state.SelectedOpportunityId is not null) return Error(_content.Message("error.opportunity.decided"));
        _state.SelectedOpportunityId = "skipped";
        _state.CurrentUiState = "Relationship";
        return GetView();
    }

    public object ReserveCard(CardRequest request)
    {
        if (_state.CurrentUiState != "MonthPlan") return Error(_content.Message("error.card.reserve_phase"));
        try
        {
            _monthlyDeck.ReserveCard(_state, request.CardId);
        }
        catch (InvalidOperationException ex)
        {
            return Error(ex.Message);
        }
        return GetView();
    }

    public object CancelReservedCard(CardRequest request)
    {
        if (_state.CurrentUiState != "MonthPlan") return Error(_content.Message("error.card.reserve_phase"));
        try
        {
            _monthlyDeck.CancelReservation(_state, request.CardId);
        }
        catch (InvalidOperationException ex)
        {
            return Error(ex.Message);
        }
        return GetView();
    }

    public object RefreshCards(RefreshCardsRequest request)
    {
        if (_state.CurrentUiState != "MonthPlan") return Error(_content.Message("error.card.refresh_phase"));
        try
        {
            _monthlyDeck.RefreshCards(_state, Cards, request.CardIds);
        }
        catch (InvalidOperationException ex)
        {
            return Error(ex.Message);
        }
        return GetView();
    }

    public object RelationshipAction(RelationshipActionRequest request)
    {
        if (!_state.Relationships.TryGetValue(request.CharacterId, out var rel) || rel.Stage == "stranger")
            return Error(_content.Message("error.relationship.unavailable"));
        var limit = RelationshipActionLimit(_state);
        if (_state.RelationshipActionsUsedThisMonth >= limit)
            return Error(_content.Message("error.relationship.used"));
        var pending = _characters.ResolveInteraction(_state, request.CharacterId, request.RelationshipActionId);
        if (!string.IsNullOrWhiteSpace(request.SceneId))
        {
            var requestedScene = _content.Scenes.FirstOrDefault(x =>
                string.Equals(x.Id, request.SceneId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (requestedScene is null)
                return Error(_content.Message("error.relationship.unavailable"));
            pending.BackgroundId = requestedScene.Id;
            pending.BackgroundPath = requestedScene.BackgroundPath;
        }
        _state.RelationshipActionsUsedThisMonth++;
        _state.Flags[$"relationship_action_{_state.CurrentMonth}"] = _state.RelationshipActionsUsedThisMonth >= limit;
        _state.RelationshipInteraction = pending;
        StartRelationshipInteractionGeneration(DeepClone(_state), _content.Character(request.CharacterId), DeepClone(pending));
        _state.CurrentUiState = "RelationshipScene";
        return GetView();
    }

    public object ResolveRelationshipChoice(RelationshipChoiceRequest request)
    {
        var interaction = _state.RelationshipInteraction;
        if (interaction is null || interaction.Source == "pending" || interaction.InteractionId != request.InteractionId)
            return Error(_content.Message("error.relationship.unavailable"));
        if (!string.IsNullOrWhiteSpace(interaction.SelectedOptionId))
            return Error(_content.Message("error.relationship.unavailable"));

        var option = interaction.InteractionOptions.FirstOrDefault(x => x.OptionId == request.OptionId);
        if (option is null)
            return Error(_content.Message("error.relationship.unavailable"));

        _characters.ApplyInteractionChoice(_state, interaction, option);
        return GetView();
    }

    public object NextMonth()
    {
        if (_state.CurrentMonth >= _state.MaxMonth)
        {
            _state.EndingId = DetermineEnding();
            _state.CurrentUiState = "Ending";
            return GetView();
        }
        _state.CurrentMonth++;
        _state.Opportunities = [];
        _state.SelectedOpportunityId = null;
        _state.CurrentMonthHand = [];
        _state.SelectedMonthCards = [];
        _state.HasRefreshedCardsThisMonth = false;
        _state.CurrentMonthEventIds = [];
        _state.LastResolution = null;
        _state.StoredAiPayloads = null;
        _state.RelationshipInteraction = null;
        _state.RelationshipActionsUsedThisMonth = 0;
        _state.Flags["burnout"] = false;
        _monthlyDeck.GenerateForMonth(_state, Cards);
        _state.CurrentUiState = "MonthStart";
        return GetView();
    }

    public object SetUi(string uiState)
    {
        if (_state.CurrentUiState == "CoreAttributeAllocation" && uiState != "CoreAttributeAllocation" && uiState != "StartMenu")
            return Error("请先确认或跳过本次核心属性加点。");
        if (uiState == "MonthPlan" && _state.CurrentMonthHand.Count == 0)
            _monthlyDeck.GenerateForMonth(_state, Cards);
        _state.CurrentUiState = uiState;
        return GetView();
    }

    public IReadOnlyList<SaveSlotInfo> ListSaves() => _saves.ListSlots();

    public object SaveGame(SaveSlotRequest request)
    {
        if (IsMenuState(_state.CurrentUiState)) return Error(_content.Message("error.save.game_only"));
        try
        {
            _saves.Save(request.Slot, _state);
        }
        catch (InvalidOperationException ex)
        {
            return Error(ex.Message);
        }
        return GetView();
    }

    private static bool IsMenuState(string uiState) =>
        uiState is "StartMenu" or "FacultySelection" or "Opening" or "Continue" or "Achievements" or "ApiSettings";

    public object LoadGame(SaveSlotRequest request)
    {
        try
        {
            _state = _saves.Load(request.Slot);
            _characters.HydrateState(_state);
            var validUiStates = new HashSet<string>
            {
                "MonthStart", "Deck", "CoreAttributeDetail", "MonthPlan", "CoreAttributeAllocation", "MonthResolution",
                "OpportunitySelection", "Relationship", "RelationshipScene", "EventScene",
                "MonthlyReview", "Archive", "Ending"
            };
            if (!validUiStates.Contains(_state.CurrentUiState))
                _state.CurrentUiState = _state.LastResolution is null ? "MonthStart" : "MonthlyReview";
            if (_state.CurrentUiState == "MonthPlan" && _state.CurrentMonthHand.Count == 0)
                _monthlyDeck.GenerateForMonth(_state, Cards);
        }
        catch (InvalidOperationException ex)
        {
            return Error(ex.Message);
        }
        return GetView();
    }

    private int Score(MonthlyOpportunityCard card)
    {
        var mpBonus = _state.Stats.MP >= 70 ? 5 : _state.Stats.MP >= 40 ? 0 : _state.Stats.MP >= 20 ? -8 : -15;
        return 50 + _state.Core.Level(card.PrimaryCoreAttribute) * 3 + mpBonus + _random.Next(-15, 16);
    }

    private void GenerateOpportunities()
    {
        var list = new List<Opportunity>();
        if (HasEvent("seminar_meeting") && _state.Relationships.GetValueOrDefault("seminar_girl")?.Stage == "acquaintance" && (_state.Core.Level("creation") >= 2 || _state.Core.Level("academic") >= 2))
            list.Add(CreateOpportunity("workshop"));
        if (HasEvent("career_center"))
            list.Add(CreateOpportunity("career_briefing"));
        if (HasEvent("campus_festival"))
            list.Add(CreateOpportunity("festival_creation"));
        _state.Opportunities = list;
    }

    private Opportunity CreateOpportunity(string id)
    {
        var content = _content.Opportunity(id);
        return new()
        {
            Id = content.Id,
            Title = content.Title,
            Description = content.Description,
            Risk = content.Risk,
            Reward = content.Reward
        };
    }

    private string DetermineEnding()
    {
        if (_state.HospitalizedCount >= 2 || _state.BurnoutCount >= 2 || _state.Stats.Money < -30000) return _content.Message("ending.collapse");
        if (_state.Core.Level("creation") >= 5 && (_state.Flags.GetValueOrDefault("festival_creation") || HasEvent("burnout")))
            return _content.Message("ending.creation_seed");
        return _content.Message("ending.normal");
    }

    private object BuildFinanceView()
    {
        var monthlyFixedExpense = MonthlyFixedExpense(_state);
        var tuitionDue = IsTuitionDue(_state);
        var tuitionDueAmount = tuitionDue ? _state.Tuition.Amount : 0;
        var totalDueThisMonth = monthlyFixedExpense + tuitionDueAmount;
        var projectedBalance = _state.Stats.Money - totalDueThisMonth;
        return new
        {
            currentMoney = _state.Stats.Money,
            rent = _state.Housing.Rent,
            housingComfort = _state.Housing.HousingComfort,
            commuteBurden = _state.Housing.CommuteBurden,
            livingCost = _state.MonthlyExpense.LivingCost,
            communicationCost = _state.MonthlyExpense.CommunicationCost,
            transportationCost = _state.MonthlyExpense.TransportationCost,
            monthlyFixedExpense,
            tuitionAmount = _state.Tuition.Amount,
            tuitionDueThisMonth = tuitionDue,
            tuitionDueAmount,
            nextTuitionMonth = _state.Tuition.NextDueMonth,
            monthsUntilTuition = MonthsUntilTuition(_state),
            totalDueThisMonth,
            projectedBalanceAfterFixed = projectedBalance,
            riskLevel = FinancialRiskLevel(projectedBalance)
        };
    }

    private object BuildRelationshipView()
    {
        var limit = RelationshipActionLimit(_state);
        return new
        {
            actionsUsed = _state.RelationshipActionsUsedThisMonth,
            actionLimit = limit,
            actionsRemaining = Math.Max(0, limit - _state.RelationshipActionsUsedThisMonth)
        };
    }

    private static int RelationshipActionLimit(GameState state)
    {
        var known = state.Relationships.Values.Where(x => x.Stage != "stranger").ToList();
        if (known.Count == 0) return 0;
        if (known.Any(x => x.Stage == "close" || x.Affection >= 55 && x.Trust >= 40) || state.Core.Level("relationship") >= 3)
            return 3;
        if (known.Any(x => x.Stage == "friend" || x.Affection >= 25 && x.Trust >= 15) || state.Core.Level("relationship") >= 1)
            return 2;
        return 1;
    }

    private void ApplyMonthlyExpenses(MonthResolution resolution)
    {
        var fixedExpense = MonthlyFixedExpense(_state);
        var tuitionPaid = IsTuitionDue(_state) ? _state.Tuition.Amount : 0;
        _state.Stats.Money -= fixedExpense + tuitionPaid;
        if (tuitionPaid > 0)
            _state.Tuition.NextDueMonth += _state.Tuition.IntervalMonths;

        resolution.FixedExpenseTotal = fixedExpense;
        resolution.TuitionPaid = tuitionPaid;

        var inCrisis = _state.Stats.Money < 0;
        _state.Flags["financial_crisis"] = inCrisis;
        if (inCrisis) _events.Trigger(_state, "financial_crisis");
    }

    private void ApplyHousingDelta(HousingDelta delta)
    {
        if (delta.RentDelta == 0 && delta.HousingComfortDelta == 0 && delta.CommuteBurdenDelta == 0) return;

        _state.Housing.Rent = Math.Max(20000, _state.Housing.Rent + delta.RentDelta);
        _state.Housing.HousingComfort = Math.Clamp(_state.Housing.HousingComfort + delta.HousingComfortDelta, 0, 100);
        _state.Housing.CommuteBurden = Math.Clamp(_state.Housing.CommuteBurden + delta.CommuteBurdenDelta, 0, 100);
    }

    private static int MonthlyFixedExpense(GameState state) =>
        state.Housing.Rent
        + state.MonthlyExpense.LivingCost
        + state.MonthlyExpense.CommunicationCost
        + state.MonthlyExpense.TransportationCost;

    private static bool IsTuitionDue(GameState state) => state.CurrentMonth >= state.Tuition.NextDueMonth;

    private static int MonthsUntilTuition(GameState state) =>
        Math.Max(0, state.Tuition.NextDueMonth - state.CurrentMonth);

    private static string FinancialRiskLevel(int projectedMoney) => projectedMoney switch
    {
        < 0 => "crisis",
        < 30000 => "pressure",
        < 80000 => "watch",
        _ => "stable"
    };

    private Dictionary<string, int> CoreSnapshot() => new()
    {
        ["academic"] = _state.Core.Academic, ["language"] = _state.Core.Language, ["career"] = _state.Core.Career,
        ["creation"] = _state.Core.Creation, ["life"] = _state.Core.Life, ["relationship"] = _state.Core.Relationship
    };

    private void AddEventLog(string eventId)
    {
        var definition = _events.Definitions.FirstOrDefault(x => x.Id == eventId);
        _state.MonthlyLogs.Add(_content.Format("event.log", definition?.Name ?? eventId, definition?.Description ?? ""));
    }

    private bool HasEvent(string id) => _state.TriggeredEventIds.Contains(id);
    private object Error(string message) => new { error = message, state = _state, cards = Cards, cardCatalog = GetCardCatalog(), labels = _content.Labels, faculties = _content.Faculties, seminars = _content.Seminars, characters = _content.Characters, finance = BuildFinanceView(), relationship = BuildRelationshipView(), saves = _saves.ListSlots() };

    private List<MonthlyOpportunityCard> ResolveSelectedCards(MonthPlanRequest request)
    {
        var selections = request.SelectedCards.Count > 0
            ? request.SelectedCards
            : request.SelectedCardIds.Select(id => new PlanCardSelection { CardId = id }).ToList();
        if (selections.Select(x => x.CardId).Distinct().Count() != selections.Count)
            throw new ArgumentException(_content.Message("error.selection.duplicate"));

        var cards = new List<MonthlyOpportunityCard>();
        foreach (var selection in selections)
        {
            try
            {
                cards.Add(_monthlyDeck.ResolveSelection(_state, selection));
            }
            catch (InvalidOperationException ex)
            {
                throw new ArgumentException(ex.Message);
            }
        }
        return cards;
    }
    private static GameState CreateInitialState(ContentService content)
    {
        var state = new GameState();
        foreach (var character in content.Characters.Where(x => x.AvailableSeminarIds.Count == 0))
        {
            state.Relationships[character.Id] = new()
            {
                CharacterId = character.Id,
                Name = character.Name,
                Stage = character.Stage,
                Affection = character.Affection,
                Trust = character.Trust,
                Mood = character.InitialMood,
                MoodValue = character.InitialMoodValue
            };
        }
        return state;
    }

    private void StartAiGeneration(GameState snapshot, MonthResolution resolution, string pendingLog)
    {
        Task.Run(() =>
        {
            AiPayloadBundle payload;
            try
            {
                payload = _ai.GenerateMonthlyPayload(snapshot, resolution);
            }
            catch (Exception ex)
            {
                payload = new AiPayloadBundle
                {
                    Source = "fallback",
                    FallbackReason = ex.Message,
                    Month = snapshot.CurrentMonth,
                    MonthlyReviewPayload = new()
                    {
                        Title = $"第 {snapshot.CurrentMonth} 月：本地结算完成",
                        Summary = "AI 演出暂时没有返回，但本地规则已经完成结算。",
                        Paragraphs = ["你可以继续查看本月的行动结果、机会和关系变化。"]
                    },
                    ArchiveMemoryPayload = new()
                    {
                        LogTitle = $"第 {snapshot.CurrentMonth} 月",
                        LogText = "AI 演出暂时没有返回，但本地规则已经完成结算。"
                    },
                    Title = $"第 {snapshot.CurrentMonth} 月：本地结算完成",
                    Summary = "AI 演出暂时没有返回，但本地规则已经完成结算。",
                    Paragraphs = ["你可以继续查看本月的行动结果、机会和关系变化。"]
                };
            }

            lock (_gate)
            {
                var finalLog = _content.Format("log.month_summary", snapshot.CurrentMonth, payload.Summary);
                var index = _state.MonthlyLogs.FindIndex(x => x == pendingLog);
                if (index >= 0) _state.MonthlyLogs[index] = finalLog;
                else if (!_state.MonthlyLogs.Contains(finalLog)) _state.MonthlyLogs.Add(finalLog);

                if (_state.CurrentMonth == snapshot.CurrentMonth && _state.LastResolution?.Month == snapshot.CurrentMonth)
                    _state.StoredAiPayloads = payload;
            }
        });
    }

    private void StartRelationshipInteractionGeneration(GameState snapshot, CharacterContent character, RelationshipInteractionPayload resolved)
    {
        Task.Run(() =>
        {
            RelationshipInteractionPayload payload;
            try
            {
                payload = _ai.GenerateRelationshipInteraction(snapshot, character, resolved);
            }
            catch (Exception ex)
            {
                resolved.Source = "fallback";
                resolved.FallbackReason = ex.Message;
                resolved.ResultText = "这次互动已经结束。演出文本暂时未能完整生成，但本地关系结算仍然有效。";
                resolved.MemoryUpdate = resolved.ResultText;
                payload = _visualNovelScenes.Normalize(resolved, character, resolved);
            }
            lock (_gate)
            {
                if (_state.RelationshipInteraction?.Source == "pending" &&
                    _state.RelationshipInteraction.InteractionId == resolved.InteractionId)
                {
                    _characters.EnrichLatestInteractionMemory(_state, payload);
                    _state.RelationshipInteraction = payload;
                }
            }
        });
    }

    private OpeningNarrationPayload CreateLocalOpening(FacultyDefinition faculty, SeminarDefinition seminar) => new()
    {
        Source = "local",
        FacultyId = faculty.Id,
        Title = _content.Format("opening.local.title", faculty.Name),
        Paragraphs =
        [
            _content.Format("opening.local.arrival", faculty.Name),
            _content.Format("opening.local.faculty", faculty.Description, faculty.Theme),
            _content.Format("opening.local.seminar", seminar.Name, seminar.Description),
            _content.Message("opening.local.evening")
        ]
    };

    private static AiPayloadBundle CreatePendingAiPayload(GameState state, MonthResolution resolution)
    {
        var summary = "本地结算已经完成，AI 演出正在后台生成。你可以先继续查看结算、机会和关系变化。";
        return new()
        {
            Source = "pending",
            Month = state.CurrentMonth,
            MonthlyReviewPayload = new()
            {
                Title = $"第 {resolution.Month} 月：演出生成中",
                Summary = summary,
                Paragraphs =
                [
                    "这不是卡死。规则结果已经返回，文本演出会在模型返回后自动替换。",
                    "如果你停在这个页面，稍等几秒会自动刷新。"
                ]
            },
            ArchiveMemoryPayload = new()
            {
                LogTitle = $"第 {resolution.Month} 月：演出生成中",
                LogText = summary
            },
            Title = $"第 {resolution.Month} 月：演出生成中",
            Summary = summary,
            Paragraphs =
            [
                "这不是卡死。规则结果已经返回，文本演出会在模型返回后自动替换。",
                "如果你停在这个页面，稍等几秒会自动刷新。"
            ],
            RelationshipTexts = state.Relationships.ToDictionary(x => x.Key, _ => "AI 演出正在生成中。")
        };
    }

    private static T DeepClone<T>(T source) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source, CloneOptions), CloneOptions)
        ?? throw new InvalidOperationException("Failed to clone game state.");
}
