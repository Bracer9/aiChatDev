using NichiryuSim;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<AiNarrationOptions>(builder.Configuration.GetSection("AiNarration"));
builder.Services.AddSingleton<GameService>();
builder.Services.AddSingleton<ContentService>();
builder.Services.AddSingleton<SaveService>();
builder.Services.AddSingleton<EventService>();
builder.Services.AddSingleton<CharacterService>();
builder.Services.AddSingleton<CardService>();
builder.Services.AddSingleton<MonthlyDeckService>();
builder.Services.AddSingleton<VisualNovelSceneService>();
builder.Services.AddSingleton<AiSettingsService>();
builder.Services.AddSingleton<PromptBuilder>();
builder.Services.AddSingleton<AiPayloadValidator>();
builder.Services.AddSingleton<MockAiNarrationService>();
builder.Services.AddHttpClient<LlmClient>();
builder.Services.AddSingleton<IAiNarrationService, AiNarrationService>();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/state", (GameService game) => game.GetView());
app.MapGet("/api/cards", (GameService game) => game.Cards);
app.MapGet("/api/cards/catalog", (GameService game) => game.GetCardCatalog());
app.MapGet("/api/saves", (GameService game) => game.ListSaves());
app.MapGet("/api/ai-settings", (AiSettingsService settings) => settings.GetPublic());
app.MapPost("/api/ai-settings", (AiSettingsRequest request, AiSettingsService settings) => new { aiSettings = settings.Save(request) });
app.MapPost("/api/ai-settings/test", () =>
{
    try
    {
        var llm = app.Services.GetRequiredService<LlmClient>();
        return Results.Json(new { message = llm.TestConnection() });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message });
    }
});
app.MapPost("/api/game/new", (NewGameRequest request, GameService game) => game.NewGame(request));
app.MapPost("/api/game/begin", (GameService game) => game.BeginFirstMonth());
app.MapPost("/api/save", (SaveSlotRequest request, GameService game) => game.SaveGame(request));
app.MapPost("/api/load", (SaveSlotRequest request, GameService game) => game.LoadGame(request));
app.MapPost("/api/month/plan", (MonthPlanRequest request, GameService game) => game.ResolveMonth(request));
app.MapPost("/api/core-attributes/allocate", (CoreAttributeAllocationRequest request, GameService game) => game.AllocateCoreAttributes(request));
app.MapPost("/api/month/card/reserve", (CardRequest request, GameService game) => game.ReserveCard(request));
app.MapPost("/api/month/card/unreserve", (CardRequest request, GameService game) => game.CancelReservedCard(request));
app.MapPost("/api/month/card/refresh", (RefreshCardsRequest request, GameService game) => game.RefreshCards(request));
app.MapPost("/api/opportunity/select", (OpportunityRequest request, GameService game) => game.SelectOpportunity(request));
app.MapPost("/api/opportunity/skip", (GameService game) => game.SkipOpportunity());
app.MapPost("/api/relationship/action", (RelationshipActionRequest request, GameService game) => game.RelationshipAction(request));
app.MapPost("/api/relationship/choice", (RelationshipChoiceRequest request, GameService game) => game.ResolveRelationshipChoice(request));
app.MapPost("/api/month/next", (GameService game) => game.NextMonth());
app.MapPost("/api/ui/{uiState}", (string uiState, GameService game) => game.SetUi(uiState));

app.MapFallbackToFile("index.html");
app.Run();
