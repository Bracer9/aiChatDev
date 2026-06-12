namespace NichiryuSim;

public sealed class EventService
{
    private readonly ContentService _content;
    private readonly CharacterService _characters;

    public EventService(ContentService content, CharacterService characters)
    {
        _content = content;
        _characters = characters;
        Definitions = content.Events;
    }

    public IReadOnlyList<EventDefinition> Definitions { get; }

    public void CheckMonthlyEvents(GameState state)
    {
        if (state.CurrentMonth == EventMonth(state, "first_report", 2)) Trigger(state, "first_report");
        if (!state.Flags.GetValueOrDefault("convenience_store_hired") && state.CurrentMonth <= 4 && HasWorkedForMoney(state))
        {
            state.Flags["convenience_store_hired"] = true;
            Trigger(state, "part_time_hired");
        }
        if (state.Core.Level("creation") >= 2 && Count(state, "arrange_music") >= 2)
            Trigger(state, "hear_melody");
        var festivalMonth = EventMonth(state, "campus_festival", 8);
        if (state.CurrentMonth >= festivalMonth && state.CurrentMonth <= festivalMonth + 2 &&
            (state.Core.Level("creation") >= 2 || state.Core.Level("relationship") >= 2))
            Trigger(state, "campus_festival");
        var careerMonth = EventMonth(state, "career_center", 3);
        if (state.CurrentMonth >= careerMonth && state.CurrentMonth <= careerMonth + 3 &&
            state.Core.Level("career") >= 1)
            Trigger(state, "career_center");
        if (state.Core.Level("creation") >= 4 &&
            (state.BurnoutCount > 0 || Count(state, "watch_animation") + Count(state, "idea_notebook") >= 6))
            Trigger(state, "ai_miku_seed");
    }

    public void Trigger(GameState state, string id, string? description = null)
    {
        if (state.TriggeredEventIds.Contains(id)) return;
        var definition = Definitions.FirstOrDefault(x => x.Id == id);
        if (definition is null) return;
        if (definition.RequiredSeminarIds.Count > 0 && !definition.RequiredSeminarIds.Contains(state.SeminarId)) return;
        if (definition.RequiredFlags.Any(flag => !state.Flags.GetValueOrDefault(flag))) return;
        state.TriggeredEventIds.Add(id);
        state.CurrentMonthEventIds.Add(id);
        state.Flags[$"event.{id}"] = true;
        state.MonthlyLogs.Add(_content.Format("event.log", definition.Name, description ?? definition.Description));
        foreach (var flag in definition.SetFlags) state.Flags[flag] = true;
        foreach (var characterId in definition.RelatedCharacterIds)
        {
            var relationship = _characters.EnsureRelationship(state, characterId);
            if (relationship.Stage == "stranger") relationship.Stage = "acquaintance";
            _characters.UpdateStageAndFlags(state, _content.Character(characterId), relationship);
        }
        _characters.RecordEventMemories(state, definition);
    }

    public string Name(string id) => Definitions.FirstOrDefault(x => x.Id == id)?.Name ?? id;

    private static int Count(GameState state, string key) => state.CardExecutionCounters.GetValueOrDefault(key);
    private static bool HasWorkedForMoney(GameState state) =>
        Count(state, "light_work") > 0
        || Count(state, "short_intensive_work") > 0
        || Count(state, "frugal_living") > 0;

    private int EventMonth(GameState state, string eventId, int defaultMonth) =>
        _content.Faculty(state.FacultyId).EventMonths.GetValueOrDefault(eventId, defaultMonth);

}
