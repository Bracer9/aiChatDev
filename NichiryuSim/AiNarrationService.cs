using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NichiryuSim;

public interface IAiNarrationService
{
    AiPayloadBundle GenerateMonthlyPayload(GameState state, MonthResolution resolution);
    RelationshipInteractionPayload GenerateRelationshipInteraction(GameState state, CharacterContent character, RelationshipInteractionPayload resolved);
}

public sealed class AiNarrationService(
    AiSettingsService settings,
    PromptBuilder promptBuilder,
    LlmClient llmClient,
    AiPayloadValidator validator,
    MockAiNarrationService fallback,
    VisualNovelSceneService visualNovelScenes,
    ILogger<AiNarrationService> logger) : IAiNarrationService
{
    public AiPayloadBundle GenerateMonthlyPayload(GameState state, MonthResolution resolution)
    {
        var mode = settings.GetEffective().Mode.Trim();
        if (mode.Equals("Mock", StringComparison.OrdinalIgnoreCase))
            return fallback.GenerateMonthlyPayload(state, resolution);

        try
        {
            var prompt = promptBuilder.BuildMonthlyPayloadPrompt(state, resolution);
            var payload = llmClient.GenerateMonthlyPayload(prompt);
            return validator.ValidateAndNormalize(payload, state, resolution);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI narration failed. Falling back to mock payload.");
            var payload = fallback.GenerateMonthlyPayload(state, resolution);
            payload.Source = "fallback";
            payload.FallbackReason = ex.Message;
            return payload;
        }
    }

    public RelationshipInteractionPayload GenerateRelationshipInteraction(GameState state, CharacterContent character, RelationshipInteractionPayload resolved)
    {
        var mode = settings.GetEffective().Mode.Trim();
        if (mode.Equals("Mock", StringComparison.OrdinalIgnoreCase))
            return fallback.GenerateRelationshipInteraction(state, character, resolved);
        try
        {
            var payload = llmClient.GenerateRelationshipInteraction(promptBuilder.BuildRelationshipInteractionPrompt(state, character, resolved), resolved);
            return visualNovelScenes.Normalize(payload, character, resolved);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI relationship interaction failed. Falling back to mock scene.");
            var payload = fallback.GenerateRelationshipInteraction(state, character, resolved);
            payload.Source = "fallback";
            payload.FallbackReason = ex.Message;
            return payload;
        }
    }
}

public sealed class MockAiNarrationService(ContentService content, EventService events, VisualNovelSceneService visualNovelScenes) : IAiNarrationService
{
    public AiPayloadBundle GenerateMonthlyPayload(GameState state, MonthResolution resolution)
    {
        var best = resolution.CoreDelta.OrderByDescending(x => x.Value).FirstOrDefault();
        var bestName = content.Label(string.IsNullOrEmpty(best.Key) ? "life" : best.Key);
        var title = content.Format("ai.month_title", state.CurrentMonth);
        var summary = content.Format("ai.month_summary", bestName);
        var paragraphs = new List<string>
        {
            content.Format("ai.month_stats", resolution.Actions.Count, resolution.HpDelta, resolution.MpDelta, resolution.MoneyDelta),
            resolution.Events.Count > 0 ? content.Message("ai.eventful") : content.Message("ai.quiet_month")
        };

        var eventPayloads = state.CurrentMonthEventIds
            .Select(id => new EventScenePayload
            {
                EventId = id,
                Title = events.Name(id),
                SceneText = content.Format("ai.event_scene", events.Name(id))
            })
            .ToList();

        var relationshipPayloads = state.Relationships
            .Select(x => new RelationshipPayload
            {
                CharacterId = x.Key,
                StatusText = RelationshipText(x.Value)
            })
            .ToList();

        var bundle = new AiPayloadBundle
        {
            Type = "monthly_ai_payload_bundle",
            Source = "mock",
            Month = state.CurrentMonth,
            MonthlyReviewPayload = new()
            {
                Title = title,
                Summary = summary,
                Paragraphs = paragraphs
            },
            EventScenePayloads = eventPayloads,
            RelationshipPayloads = relationshipPayloads,
            OpportunityPayloads = state.Opportunities
                .Select(x => new OpportunityPayload { OpportunityId = x.Id, FlavorText = x.Description })
                .ToList(),
            ArchiveMemoryPayload = new()
            {
                LogTitle = title,
                LogText = summary
            },
            Title = title,
            Summary = summary,
            Paragraphs = paragraphs,
            EventScenes = eventPayloads.Select(x => x.SceneText).ToList(),
            RelationshipTexts = relationshipPayloads.ToDictionary(x => x.CharacterId, x => x.StatusText)
        };

        return bundle;
    }

    public RelationshipInteractionPayload GenerateRelationshipInteraction(GameState state, CharacterContent character, RelationshipInteractionPayload resolved)
    {
        resolved.Source = "mock";
        resolved.Title = resolved.ActionId == "support" ? "认真听完的话" : "走廊里的几分钟";
        resolved.Mood = resolved.StageAfter == "close" ? "不必解释的默契" : "距离稍微缩短";
        resolved.SceneText = resolved.ActionId == "support"
            ? $"你没有急着给建议，只是在{character.Name}停顿时继续等着。对方起初只说了几句近况，后来才把真正困扰自己的部分慢慢补上。谈话结束时，桌上的饮料已经失去凉意，但那段没有被打断的时间似乎比答案更重要。"
            : $"你在课程结束后的走廊叫住{character.Name}，从一件无关紧要的小事开始聊起。话题绕过课表、天气和最近忙乱的日程，最后停在一个只有你们会记住的细节上。分别时，对方回头补了一句话，让这次普通闲聊多留了几秒余温。";
        resolved.ResultText = resolved.ActionId == "support"
            ? "你认真听完了对方想说的话。"
            : "一次普通闲聊，让你们之间的距离稍微缩短。";
        resolved.MemoryUpdate = resolved.ResultText;
        return visualNovelScenes.Normalize(resolved, character, resolved);
    }

    private string RelationshipText(CharacterRelationship rel) => rel.Stage switch
    {
        "stranger" => content.Message("relationship.stranger"),
        "acquaintance" => content.Message("relationship.acquaintance"),
        "friend" => content.Message("relationship.friend"),
        "close" => content.Message("relationship.close"),
        _ => content.Message("relationship.default")
    };
}

public sealed class PromptBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _systemPrompt;
    private readonly string _relationshipSystemPrompt;
    private readonly ContentService _content;

    public PromptBuilder(IWebHostEnvironment environment, ContentService content)
    {
        _content = content;
        var root = environment.ContentRootPath;
        _systemPrompt = File.ReadAllText(Path.Combine(root, "Prompts", "monthly_payload_system_prompt.txt"));
        _relationshipSystemPrompt = File.ReadAllText(Path.Combine(root, "Prompts", "relationship_interaction_system_prompt.txt"));
    }

    public string SystemPrompt => _systemPrompt;
    public string RelationshipSystemPrompt => _relationshipSystemPrompt;

    public string BuildRelationshipInteractionPrompt(GameState state, CharacterContent character, RelationshipInteractionPayload resolved) =>
        JsonSerializer.Serialize(new
        {
            task = "根据已经由本地规则结算的人物关系变化，生成一次即时互动场景。",
            month = state.CurrentMonth,
            facultyId = state.FacultyId,
            seminarId = state.SeminarId,
            character,
            characterDefaultSceneId = character.DefaultSceneId,
            availableScenes = _content.Scenes.Select(scene => new
            {
                scene.Id,
                scene.Name,
                scene.Description,
                scene.BackgroundPath,
                scene.Tags
            }),
            relationship = RelationshipContext(state.Relationships.GetValueOrDefault(character.Id)),
            resolved,
            recentEvents = state.CurrentMonthEventIds,
            requiredOutput = "只返回合法 JSON，不要使用 Markdown 代码块。"
        }, JsonOptions);

    public string BuildMonthlyPayloadPrompt(GameState state, MonthResolution resolution)
    {
        var input = new
        {
            task = "根据本地规则已经结算完毕的本月状态，生成一个 monthly_ai_payload_bundle JSON 对象。",
            month = state.CurrentMonth,
            facultyId = state.FacultyId,
            seminarId = state.SeminarId,
            statsAfterResolution = state.Stats,
            housing = state.Housing,
            monthlyExpense = state.MonthlyExpense,
            tuition = state.Tuition,
            coreAttributes = state.Core,
            unspentLifeExperiencePoints = state.UnspentLifeExperiencePoints,
            totalLifeExperiencePointsEarned = state.TotalLifeExperiencePointsEarned,
            flags = state.Flags,
            triggeredEventIds = state.TriggeredEventIds,
            currentMonthEventIds = state.CurrentMonthEventIds,
            selectedCards = state.SelectedMonthCards.Select(card => new
            {
                card.CardId,
                card.Name,
                card.CardType,
                card.PrimaryCoreAttribute,
                card.CoreExpDelta,
                card.Rarity,
                card.MeaningText,
                card.HpDelta,
                card.MpDelta,
                card.MoneyDelta,
                card.CardTags,
                card.CustomNote,
                card.HousingDelta,
                card.TuitionDelayMonths
            }),
            monthResolution = resolution,
            opportunities = state.Opportunities.Select(x => new
            {
                x.Id,
                x.Title,
                x.Description,
                x.Risk,
                x.Reward,
                x.Selected
            }),
            relationships = state.Relationships.Values.Select(x => new
            {
                profile = _content.Character(x.CharacterId),
                state = RelationshipContext(x)
            }),
            requiredOutput = "只返回合法 JSON，不要使用 Markdown 代码块。"
        };

        return JsonSerializer.Serialize(input, JsonOptions);
    }

    private static object? RelationshipContext(CharacterRelationship? relationship)
    {
        if (relationship is null) return null;
        return new
        {
            relationship.CharacterId,
            relationship.Name,
            relationship.Stage,
            relationship.Affection,
            relationship.Trust,
            relationship.Mood,
            relationship.MoodValue,
            relationship.InteractionCount,
            relationship.LastInteractionMonth,
            relationship.LastActionId,
            importantMemories = relationship.Memories
                .OrderByDescending(x => x.Importance)
                .ThenByDescending(x => x.Month)
                .Take(8)
        };
    }
}

public sealed class LlmClient(HttpClient httpClient, AiSettingsService settings, PromptBuilder promptBuilder)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public AiPayloadBundle GenerateMonthlyPayload(string userPrompt)
    {
        var config = settings.GetEffective();
        if (string.IsNullOrWhiteSpace(config.Model))
            throw new InvalidOperationException("AiNarration:Model is required when Mode is not Mock.");

        var apiKey = ResolveApiKey(config);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("AI API key is missing. Set AiNarration:ApiKey, NICHIRYU_AI_API_KEY, or OPENAI_API_KEY.");

        var request = new
        {
            model = config.Model,
            messages = new[]
            {
                new { role = "system", content = promptBuilder.SystemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.75,
            max_tokens = 3600,
            thinking = new { type = "disabled" },
            response_format = new { type = "json_object" }
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, config.TimeoutSeconds)));
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, config.Endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json")
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = httpClient.SendAsync(requestMessage, cts.Token).GetAwaiter().GetResult();
        var responseText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        var content = ExtractMessageContent(responseText);
        var json = StripJsonFence(content);
        var payload = JsonSerializer.Deserialize<AiPayloadBundle>(json, JsonOptions)
            ?? throw new InvalidOperationException("AI response could not be parsed as AiPayloadBundle.");
        payload.Source = config.Model;
        payload.Usage = ExtractUsage(responseText);
        return payload;
    }

    public RelationshipInteractionPayload GenerateRelationshipInteraction(string userPrompt, RelationshipInteractionPayload resolved)
    {
        try
        {
            return SendRelationshipInteraction(userPrompt, resolved, 0.85);
        }
        catch (Exception firstError)
        {
            var retryPrompt = $"{userPrompt}\n\n上一次输出未能通过结构校验。请重新生成 visual_novel_scene，并严格原样复制 resolved 中的 characterId、actionId、affectionDelta、trustDelta、stageBefore、stageAfter。lines 必须包含旁白、玩家和 NPC 台词；每句 NPC 台词必须包含 expression。只返回合法 JSON。";
            try
            {
                return SendRelationshipInteraction(retryPrompt, resolved, 0.35);
            }
            catch (Exception retryError)
            {
                throw new InvalidOperationException($"关系互动 AI 两次生成均失败。首次：{firstError.Message}；重试：{retryError.Message}", retryError);
            }
        }
    }

    private RelationshipInteractionPayload SendRelationshipInteraction(string userPrompt, RelationshipInteractionPayload resolved, double temperature)
    {
        var config = settings.GetEffective();
        var apiKey = ResolveApiKey(config);
        if (string.IsNullOrWhiteSpace(config.Model) || string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("AI model or API key is missing.");

        var request = new
        {
            model = config.Model,
            messages = new[]
            {
                new { role = "system", content = promptBuilder.RelationshipSystemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature,
            max_tokens = 1800,
            thinking = new { type = "disabled" },
            response_format = new { type = "json_object" }
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, config.TimeoutSeconds)));
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, config.Endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json")
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = httpClient.SendAsync(requestMessage, cts.Token).GetAwaiter().GetResult();
        var responseText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        var payload = JsonSerializer.Deserialize<RelationshipInteractionPayload>(StripJsonFence(ExtractMessageContent(responseText)), JsonOptions)
            ?? throw new InvalidOperationException("AI response could not be parsed as relationship interaction.");
        if (payload.CharacterId != resolved.CharacterId || payload.ActionId != resolved.ActionId ||
            payload.AffectionDelta != resolved.AffectionDelta || payload.TrustDelta != resolved.TrustDelta ||
            payload.StageBefore != resolved.StageBefore || payload.StageAfter != resolved.StageAfter)
            throw new InvalidOperationException("AI relationship interaction changed resolved game state.");
        if (payload.Lines is null || payload.Lines.Count == 0)
            throw new InvalidOperationException("AI relationship interaction contains no dialogue lines.");
        if (payload.Lines.Any(x => x.LineType is not ("narration" or "npc" or "player") || string.IsNullOrWhiteSpace(x.Text)))
            throw new InvalidOperationException("AI relationship interaction contains invalid dialogue lines.");
        if (payload.Lines.Any(x => x.LineType == "npc" && string.IsNullOrWhiteSpace(x.Expression)))
            throw new InvalidOperationException("Every NPC line must include an expression.");

        payload.Title = (payload.Title ?? "").Trim();
        payload.SceneText = (payload.SceneText ?? "").Trim();
        payload.Mood = (payload.Mood ?? "").Trim();
        payload.Title = string.IsNullOrWhiteSpace(payload.Title) ? "这次相处留下的片段" : payload.Title;
        payload.Mood = string.IsNullOrWhiteSpace(payload.Mood) ? "距离稍微变化" : payload.Mood;
        payload.SceneText = string.IsNullOrWhiteSpace(payload.SceneText)
            ? "这次互动没有留下完整的语言记录，但对方的回应和短暂的停顿已经改变了你们之间的距离。"
            : payload.SceneText;
        payload.Source = config.Model;
        payload.InteractionId = resolved.InteractionId;
        payload.Usage = ExtractUsage(responseText);
        return payload;
    }

    public string TestConnection()
    {
        var config = settings.GetEffective();
        if (config.Mode.Equals("Mock", StringComparison.OrdinalIgnoreCase))
            return "当前是 Mock 模式，不会调用真实 API。";
        if (string.IsNullOrWhiteSpace(config.Model))
            throw new InvalidOperationException("请选择模型。");

        var apiKey = ResolveApiKey(config);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("请先填写 API Key。");

        var request = new
        {
            model = config.Model,
            messages = new[]
            {
                new { role = "system", content = "只返回合法 JSON。" },
                new { role = "user", content = "{\"ping\":\"ok\"} 请只返回 {\"ok\":true}。" }
            },
            max_tokens = 64,
            temperature = 0,
            thinking = new { type = "disabled" },
            response_format = new { type = "json_object" }
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, Math.Min(30, config.TimeoutSeconds))));
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, config.Endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json")
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = httpClient.SendAsync(requestMessage, cts.Token).GetAwaiter().GetResult();
        var responseText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        var content = ExtractMessageContent(responseText);
        var json = StripJsonFence(content);
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("AI 返回了空内容。请确认模型支持 non-thinking JSON 输出，或稍后重试。");
        using var _ = JsonDocument.Parse(json);
        return $"连接成功：{config.Model}";
    }

    private static string ResolveApiKey(AiNarrationOptions config) =>
        !string.IsNullOrWhiteSpace(config.ApiKey) ? config.ApiKey :
        Environment.GetEnvironmentVariable("NICHIRYU_AI_API_KEY") ??
        Environment.GetEnvironmentVariable("OPENAI_API_KEY") ??
        "";

    private static string ExtractMessageContent(string responseText)
    {
        using var doc = JsonDocument.Parse(responseText);
        var root = doc.RootElement;
        var choices = root.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
            throw new InvalidOperationException("AI response contains no choices.");

        var message = choices[0].GetProperty("message");
        if (message.TryGetProperty("content", out var content) && !string.IsNullOrWhiteSpace(content.GetString()))
            return content.GetString()!;

        var finishReason = choices[0].TryGetProperty("finish_reason", out var finish) ? finish.GetString() : "unknown";
        throw new InvalidOperationException($"AI response message content is empty. finish_reason={finishReason}");
    }

    private static AiUsageSnapshot? ExtractUsage(string responseText)
    {
        using var doc = JsonDocument.Parse(responseText);
        var root = doc.RootElement;
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return new()
        {
            Model = root.TryGetProperty("model", out var model) ? model.GetString() ?? "" : "",
            PromptTokens = GetInt(usage, "prompt_tokens"),
            CompletionTokens = GetInt(usage, "completion_tokens"),
            TotalTokens = GetInt(usage, "total_tokens"),
            PromptCacheHitTokens = GetInt(usage, "prompt_cache_hit_tokens"),
            PromptCacheMissTokens = GetInt(usage, "prompt_cache_miss_tokens")
        };
    }

    private static int GetInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    private static string StripJsonFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;

        var firstNewLine = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (firstNewLine < 0 || lastFence <= firstNewLine) return trimmed;

        return trimmed[(firstNewLine + 1)..lastFence].Trim();
    }

}

public sealed class AiPayloadValidator
{
    public AiPayloadBundle ValidateAndNormalize(AiPayloadBundle bundle, GameState state, MonthResolution resolution)
    {
        if (!bundle.Type.Equals("monthly_ai_payload_bundle", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("AI payload type is invalid.");
        if (bundle.Month != state.CurrentMonth)
            throw new InvalidOperationException("AI payload month does not match current month.");

        NormalizeReview(bundle);
        NormalizeEvents(bundle, state);
        NormalizeRelationships(bundle, state);
        NormalizeOpportunities(bundle, state);
        NormalizeArchive(bundle);
        ValidateNarrationLanguage(bundle);

        if (string.IsNullOrWhiteSpace(bundle.Title) || string.IsNullOrWhiteSpace(bundle.Summary))
            throw new InvalidOperationException("AI payload monthly review is incomplete.");
        if (bundle.Paragraphs.Count == 0)
            bundle.Paragraphs.Add($"本月完成 {resolution.Actions.Count} 个行动，结果已经由本地规则结算。");

        return bundle;
    }

    private static void NormalizeReview(AiPayloadBundle bundle)
    {
        bundle.MonthlyReviewPayload ??= new()
        {
            Title = bundle.Title,
            Summary = bundle.Summary,
            Paragraphs = bundle.Paragraphs
        };

        bundle.Title = bundle.MonthlyReviewPayload.Title.Trim();
        bundle.Summary = bundle.MonthlyReviewPayload.Summary.Trim();
        bundle.Paragraphs = bundle.MonthlyReviewPayload.Paragraphs
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Take(4)
            .ToList();
    }

    private static void NormalizeEvents(AiPayloadBundle bundle, GameState state)
    {
        var allowed = state.CurrentMonthEventIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        bundle.EventScenePayloads = bundle.EventScenePayloads
            .Where(x => allowed.Contains(x.EventId) && !string.IsNullOrWhiteSpace(x.SceneText))
            .Select(x => new EventScenePayload
            {
                EventId = x.EventId,
                Title = x.Title.Trim(),
                SceneText = x.SceneText.Trim()
            })
            .ToList();

        bundle.EventScenes = bundle.EventScenePayloads.Select(x => x.SceneText).ToList();
    }

    private static void NormalizeRelationships(AiPayloadBundle bundle, GameState state)
    {
        var allowed = state.Relationships.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        bundle.RelationshipPayloads = bundle.RelationshipPayloads
            .Where(x => allowed.Contains(x.CharacterId) && !string.IsNullOrWhiteSpace(x.StatusText))
            .Select(x => new RelationshipPayload
            {
                CharacterId = x.CharacterId,
                StatusText = x.StatusText.Trim()
            })
            .ToList();

        bundle.RelationshipTexts = bundle.RelationshipPayloads
            .GroupBy(x => x.CharacterId)
            .ToDictionary(x => x.Key, x => x.First().StatusText);

        foreach (var (id, text) in bundle.RelationshipTexts)
        {
            if (!allowed.Contains(id) || string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("AI payload relationship text is invalid.");
        }
    }

    private static void NormalizeOpportunities(AiPayloadBundle bundle, GameState state)
    {
        var allowed = state.Opportunities.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bundle.OpportunityPayloads = bundle.OpportunityPayloads
            .Where(x => allowed.Contains(x.OpportunityId) && !string.IsNullOrWhiteSpace(x.FlavorText))
            .Select(x => new OpportunityPayload
            {
                OpportunityId = x.OpportunityId,
                FlavorText = x.FlavorText.Trim()
            })
            .ToList();
    }

    private static void NormalizeArchive(AiPayloadBundle bundle)
    {
        bundle.ArchiveMemoryPayload ??= new()
        {
            LogTitle = bundle.Title,
            LogText = bundle.Summary
        };

        bundle.ArchiveMemoryPayload.LogTitle = string.IsNullOrWhiteSpace(bundle.ArchiveMemoryPayload.LogTitle)
            ? bundle.Title
            : bundle.ArchiveMemoryPayload.LogTitle.Trim();
        bundle.ArchiveMemoryPayload.LogText = string.IsNullOrWhiteSpace(bundle.ArchiveMemoryPayload.LogText)
            ? bundle.Summary
            : bundle.ArchiveMemoryPayload.LogText.Trim();
    }

    private static void ValidateNarrationLanguage(AiPayloadBundle bundle)
    {
        var texts = new List<string?>
        {
            bundle.Title,
            bundle.Summary,
            bundle.ArchiveMemoryPayload?.LogTitle,
            bundle.ArchiveMemoryPayload?.LogText
        };

        texts.AddRange(bundle.Paragraphs);
        texts.AddRange(bundle.EventScenePayloads.Select(x => x.Title));
        texts.AddRange(bundle.EventScenePayloads.Select(x => x.SceneText));
        texts.AddRange(bundle.RelationshipPayloads.Select(x => x.StatusText));
        texts.AddRange(bundle.OpportunityPayloads.Select(x => x.FlavorText));

        if (texts.Where(x => !string.IsNullOrWhiteSpace(x)).Any(IsPredominantlyJapanese))
            throw new InvalidOperationException("AI payload contains a long passage written primarily in Japanese. Please generate the narration mainly in natural Simplified Chinese.");
    }

    private static bool IsPredominantlyJapanese(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var kana = text.Count(ch => ch is >= '\u3040' and <= '\u30ff');
        var languageCharacters = text.Count(ch =>
            ch is >= '\u3040' and <= '\u30ff'
            || ch is >= '\u3400' and <= '\u9fff'
            || char.IsLetter(ch));
        return kana >= 30 && languageCharacters > 0 && kana * 4 >= languageCharacters;
    }
}
