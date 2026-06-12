using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NichiryuSim;

public sealed class AiSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsPath;
    private readonly AiNarrationOptions _defaults;
    private readonly object _lock = new();

    public AiSettingsService(IWebHostEnvironment environment, IOptions<AiNarrationOptions> defaults)
    {
        _defaults = defaults.Value;
        var saveRoot = Path.Combine(environment.ContentRootPath, "Saves");
        Directory.CreateDirectory(saveRoot);
        _settingsPath = Path.Combine(saveRoot, "ai-settings.json");
    }

    public AiNarrationOptions GetEffective()
    {
        lock (_lock)
        {
            var saved = ReadSaved();
            return new()
            {
                Mode = NormalizeMode(string.IsNullOrWhiteSpace(saved.Mode) ? _defaults.Mode : saved.Mode),
                Endpoint = NormalizeEndpoint(
                    string.IsNullOrWhiteSpace(saved.Mode) ? _defaults.Mode : saved.Mode,
                    string.IsNullOrWhiteSpace(saved.Endpoint) ? _defaults.Endpoint : saved.Endpoint),
                Model = string.IsNullOrWhiteSpace(saved.Model) ? _defaults.Model : saved.Model,
                ApiKey = string.IsNullOrWhiteSpace(saved.ApiKey) ? _defaults.ApiKey : saved.ApiKey,
                TimeoutSeconds = saved.TimeoutSeconds > 0 ? saved.TimeoutSeconds : _defaults.TimeoutSeconds
            };
        }
    }

    public AiSettingsView GetPublic()
    {
        var effective = GetEffective();
        return new()
        {
            Mode = effective.Mode,
            Endpoint = effective.Endpoint,
            Model = effective.Model,
            HasApiKey = !string.IsNullOrWhiteSpace(effective.ApiKey),
            MaskedApiKey = MaskKey(effective.ApiKey),
            TimeoutSeconds = effective.TimeoutSeconds,
            ModelOptions = ModelOptions()
        };
    }

    public AiSettingsView Save(AiSettingsRequest request)
    {
        lock (_lock)
        {
            var current = ReadSaved();
            var mode = NormalizeMode(request.Mode);
            var next = new AiNarrationOptions
            {
                Mode = mode,
                Endpoint = NormalizeEndpoint(mode, request.Endpoint),
                Model = string.IsNullOrWhiteSpace(request.Model) ? "deepseek-v4-flash" : request.Model.Trim(),
                ApiKey = request.ClearApiKey ? "" : string.IsNullOrWhiteSpace(request.ApiKey) ? current.ApiKey : request.ApiKey.Trim(),
                TimeoutSeconds = request.TimeoutSeconds is >= 5 and <= 120 ? request.TimeoutSeconds : 45
            };

            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(next, JsonOptions));
        }

        return GetPublic();
    }

    private AiNarrationOptions ReadSaved()
    {
        if (!File.Exists(_settingsPath)) return new();
        var json = File.ReadAllText(_settingsPath);
        return JsonSerializer.Deserialize<AiNarrationOptions>(json, JsonOptions) ?? new();
    }

    private static string NormalizeMode(string mode)
    {
        if (mode.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase)) return "DeepSeek";
        if (mode.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase)) return "OpenAICompatible";
        return "Mock";
    }

    private static string NormalizeEndpoint(string mode, string endpoint)
    {
        var value = string.IsNullOrWhiteSpace(endpoint) ? "" : endpoint.Trim();
        if (mode.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(value) || value.Contains("api.openai.com", StringComparison.OrdinalIgnoreCase)))
            return "https://api.deepseek.com/chat/completions";
        return string.IsNullOrWhiteSpace(value) ? "https://api.deepseek.com/chat/completions" : value;
    }

    private static List<AiModelOption> ModelOptions() =>
    [
        new()
        {
            Label = "V4 Flash",
            Model = "deepseek-v4-flash",
            Description = "更省 token，适合日常月度演出。"
        },
        new()
        {
            Label = "V4 Pro",
            Model = "deepseek-v4-pro",
            Description = "更强，适合重要剧情月或复杂事件。"
        }
    ];

    private static string MaskKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "";
        if (key.Length <= 12) return "********";
        return $"{key[..6]}...{key[^4..]}";
    }
}
