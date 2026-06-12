namespace NichiryuSim;

public sealed class MonthlyDeckService(ContentService content, CardService cards)
{
    private readonly Random _random = new();

    public void GenerateForMonth(GameState state, IReadOnlyList<CardDefinition> deck)
    {
        var hand = new List<MonthlyOpportunityCard>();
        if (state.ReservedCardForNextMonth is { } reserved && reserved.ExpireMonth == state.CurrentMonth)
        {
            reserved.IsPinnedFromLastMonth = true;
            reserved.IsReservedForNextMonth = false;
            hand.Add(reserved);
            state.ReservedCardForNextMonth = null;
        }
        else if (state.ReservedCardForNextMonth?.ExpireMonth < state.CurrentMonth)
        {
            state.ReservedCardForNextMonth = null;
        }

        var unlocked = deck
            .Where(card => cards.GetUnlockReason(card, state) is null)
            .Select(ToMonthlyCard)
            .ToList();

        var drawCount = Math.Max(1, state.EffectiveMonthlyDrawCount);
        AddMany(hand, FinancePressureCards(state, unlocked), Math.Min(1, drawCount));
        AddMany(hand, unlocked.Where(x => x.Rarity == "Special").OrderBy(_ => _random.Next()), Math.Min(1, drawCount));
        AddMany(hand, unlocked.Where(x => x.Rarity == "Rare").OrderBy(_ => _random.Next()), Math.Min(1, drawCount));
        AddMany(hand, unlocked.OrderBy(_ => _random.Next()), drawCount);

        state.CurrentMonthHand = hand.Take(drawCount).ToList();
        state.SelectedMonthCards = [];
        state.HasRefreshedCardsThisMonth = false;
    }

    public MonthlyOpportunityCard? FindCard(GameState state, string cardId) =>
        state.CurrentMonthHand.FirstOrDefault(x => x.CardId == cardId);

    public MonthlyOpportunityCard ResolveSelection(GameState state, PlanCardSelection selection)
    {
        var source = FindCard(state, selection.CardId) ?? throw new InvalidOperationException(content.Message("error.card.missing"));
        if (source.LockedReason is not null) throw new InvalidOperationException(source.LockedReason);
        if (source.IsReservedForNextMonth) throw new InvalidOperationException(content.Message("error.card.reserved_next_month"));

        var card = Clone(source);
        card.IsSelected = true;
        card.CustomNote = selection.CustomNote;
        return card;
    }

    public void ReserveCard(GameState state, string cardId)
    {
        ClearGhostReservation(state);
        var card = state.CurrentMonthHand.FirstOrDefault(x => x.CardId == cardId)
            ?? throw new InvalidOperationException(content.Message("error.card.reserve_current_only"));
        if (card.LockedReason is not null) throw new InvalidOperationException(content.Message("error.card.locked_cannot_reserve"));
        if (card.IsSelected) throw new InvalidOperationException(content.Message("error.card.selected_cannot_reserve"));
        if (state.ReservedCardForNextMonth is not null) throw new InvalidOperationException(content.Message("error.card.reserve_limit"));

        var reserved = Clone(card);
        reserved.IsReservedForNextMonth = true;
        reserved.ExpireMonth = state.CurrentMonth + 1;
        state.ReservedCardForNextMonth = reserved;
        card.IsReservedForNextMonth = true;
    }

    public void CancelReservation(GameState state, string cardId)
    {
        var card = state.CurrentMonthHand.FirstOrDefault(x => x.CardId == cardId)
            ?? throw new InvalidOperationException(content.Message("error.card.unreserve_current_only"));
        if (!card.IsReservedForNextMonth) throw new InvalidOperationException(content.Message("error.card.not_reserved"));

        card.IsReservedForNextMonth = false;
        if (state.ReservedCardForNextMonth?.CardId == cardId)
            state.ReservedCardForNextMonth = null;
        else
            ClearGhostReservation(state);
    }

    public void RefreshCards(GameState state, IReadOnlyList<CardDefinition> deck, List<string> cardIds)
    {
        if (state.HasRefreshedCardsThisMonth) throw new InvalidOperationException(content.Message("error.card.already_refreshed"));
        if (cardIds.Count < 1 || cardIds.Count > state.MonthlySwitchLimit)
            throw new InvalidOperationException(content.Format("error.card.refresh_count", state.MonthlySwitchLimit));
        if (state.Stats.MP < 5) throw new InvalidOperationException(content.Message("error.card.refresh_mp"));
        if (cardIds.Distinct().Count() != cardIds.Count) throw new InvalidOperationException(content.Message("error.card.refresh_duplicate"));

        var indexes = cardIds.Select(id => state.CurrentMonthHand.FindIndex(x => x.CardId == id)).ToList();
        if (indexes.Any(x => x < 0)) throw new InvalidOperationException(content.Message("error.card.refresh_current_only"));
        if (indexes.Any(i => state.CurrentMonthHand[i].IsReservedForNextMonth || state.CurrentMonthHand[i].IsPinnedFromLastMonth))
            throw new InvalidOperationException(content.Message("error.card.reserved_cannot_refresh"));
        if (indexes.Any(i => state.CurrentMonthHand[i].IsSelected))
            throw new InvalidOperationException(content.Message("error.card.selected_cannot_refresh"));

        var usedIds = state.CurrentMonthHand.Select(x => x.CardId).ToHashSet();
        var pool = deck
            .Where(card => cards.GetUnlockReason(card, state) is null && !usedIds.Contains(card.CardId))
            .OrderBy(_ => _random.Next())
            .Select(ToMonthlyCard)
            .ToList();

        foreach (var index in indexes)
        {
            var replacement = pool.FirstOrDefault(x => !usedIds.Contains(x.CardId))
                ?? throw new InvalidOperationException(content.Message("error.card.no_replacement"));
            usedIds.Add(replacement.CardId);
            state.CurrentMonthHand[index] = replacement;
        }

        state.Stats.MP -= 5;
        state.HasRefreshedCardsThisMonth = true;
    }

    private IEnumerable<MonthlyOpportunityCard> FinancePressureCards(GameState state, List<MonthlyOpportunityCard> unlocked)
    {
        var pressure = state.Stats.Money < 60000
            || state.Stats.Money - MonthlyFixedExpense(state) < 30000
            || MonthsUntilTuition(state) <= 2;
        return pressure
            ? unlocked.Where(x => x.CardTags.Contains("finance")).OrderBy(_ => _random.Next())
            : [];
    }

    private static MonthlyOpportunityCard ToMonthlyCard(CardDefinition card) => new()
    {
        CardId = card.CardId,
        Name = card.Name,
        Description = card.Description,
        MeaningText = card.MeaningText,
        CardType = card.CardType,
        PrimaryCoreAttribute = card.PrimaryCoreAttribute,
        CoreExpDelta = card.CoreExpDelta,
        HpDelta = card.HpDelta,
        MpDelta = card.MpDelta,
        MoneyDelta = card.MoneyDelta,
        Rarity = card.Rarity,
        UnlockRequirements = CopyUnlockRequirements(card.UnlockRequirements),
        CardTags = card.CardTags.ToList(),
        HousingDelta = CopyHousingDelta(card.HousingDelta),
        TuitionDelayMonths = card.TuitionDelayMonths,
        PossibleEventIds = card.PossibleEventIds.ToList(),
        RelatedCharacterId = card.RelatedCharacterId,
        IsInitialCard = card.IsInitialCard,
        InitialFacultyIds = card.InitialFacultyIds.ToList(),
        IsHiddenUntilUnlocked = card.IsHiddenUntilUnlocked,
        CannotRepeatConsecutively = card.CannotRepeatConsecutively
    };

    private static void AddMany(List<MonthlyOpportunityCard> target, IEnumerable<MonthlyOpportunityCard> cards, int maxCount)
    {
        foreach (var card in cards.Where(x => target.All(y => y.CardId != x.CardId)).Take(maxCount))
            target.Add(card);
    }

    private static void ClearGhostReservation(GameState state)
    {
        if (state.ReservedCardForNextMonth is null) return;
        var visible = state.CurrentMonthHand.Any(x => x.IsReservedForNextMonth);
        if (!visible || state.ReservedCardForNextMonth.ExpireMonth <= state.CurrentMonth)
            state.ReservedCardForNextMonth = null;
    }

    private static MonthlyOpportunityCard Clone(MonthlyOpportunityCard source) => new()
    {
        CardId = source.CardId,
        Name = source.Name,
        Description = source.Description,
        MeaningText = source.MeaningText,
        CardType = source.CardType,
        PrimaryCoreAttribute = source.PrimaryCoreAttribute,
        CoreExpDelta = source.CoreExpDelta,
        HpDelta = source.HpDelta,
        MpDelta = source.MpDelta,
        MoneyDelta = source.MoneyDelta,
        Rarity = source.Rarity,
        UnlockRequirements = CopyUnlockRequirements(source.UnlockRequirements),
        CardTags = source.CardTags.ToList(),
        HousingDelta = CopyHousingDelta(source.HousingDelta),
        TuitionDelayMonths = source.TuitionDelayMonths,
        PossibleEventIds = source.PossibleEventIds.ToList(),
        LockedReason = source.LockedReason,
        RelatedCharacterId = source.RelatedCharacterId,
        IsInitialCard = source.IsInitialCard,
        InitialFacultyIds = source.InitialFacultyIds.ToList(),
        IsHiddenUntilUnlocked = source.IsHiddenUntilUnlocked,
        CannotRepeatConsecutively = source.CannotRepeatConsecutively,
        IsLimited = source.IsLimited,
        ExpireMonth = source.ExpireMonth,
        IsPinnedFromLastMonth = source.IsPinnedFromLastMonth,
        IsSelected = source.IsSelected,
        IsReservedForNextMonth = source.IsReservedForNextMonth,
        CustomNote = source.CustomNote
    };

    private static CardUnlockRequirements CopyUnlockRequirements(CardUnlockRequirements source) => new()
    {
        RequiredCoreAttributeLevels = source.RequiredCoreAttributeLevels.ToDictionary(),
        RequiredFlags = source.RequiredFlags.ToList(),
        ForbiddenFlags = source.ForbiddenFlags.ToList(),
        RequiredFaculty = source.RequiredFaculty,
        RequiredSeminar = source.RequiredSeminar,
        RequiredCharacterId = source.RequiredCharacterId,
        RequiredRelationshipStage = source.RequiredRelationshipStage
    };

    private static HousingDelta CopyHousingDelta(HousingDelta source) => new()
    {
        RentDelta = source.RentDelta,
        HousingComfortDelta = source.HousingComfortDelta,
        CommuteBurdenDelta = source.CommuteBurdenDelta
    };

    private static int MonthlyFixedExpense(GameState state) =>
        state.Housing.Rent + state.MonthlyExpense.LivingCost
        + state.MonthlyExpense.CommunicationCost + state.MonthlyExpense.TransportationCost;

    private static int MonthsUntilTuition(GameState state) =>
        Math.Max(0, state.Tuition.NextDueMonth - state.CurrentMonth);
}
