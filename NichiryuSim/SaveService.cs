using System.Text.Json;

namespace NichiryuSim;

public sealed class SaveService
{
    public const int SlotCount = 20;

    private readonly string _saveRoot;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public SaveService(IWebHostEnvironment environment)
    {
        _saveRoot = Path.Combine(environment.ContentRootPath, "Saves");
    }

    public IReadOnlyList<SaveSlotInfo> ListSlots() =>
        Enumerable.Range(1, SlotCount).Select(ReadSlotInfo).ToList();

    public void Save(int slot, GameState state)
    {
        EnsureSlot(slot);
        Directory.CreateDirectory(_saveRoot);

        var file = new SaveFile
        {
            Slot = slot,
            SavedAt = DateTimeOffset.Now,
            State = state
        };
        File.WriteAllText(SlotPath(slot), JsonSerializer.Serialize(file, _jsonOptions));
    }

    public GameState Load(int slot)
    {
        EnsureSlot(slot);
        var path = SlotPath(slot);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Save slot {slot:00} is empty.");

        var file = JsonSerializer.Deserialize<SaveFile>(File.ReadAllText(path))
            ?? throw new InvalidOperationException($"Save slot {slot:00} is invalid.");
        return file.State;
    }

    private SaveSlotInfo ReadSlotInfo(int slot)
    {
        var path = SlotPath(slot);
        if (!File.Exists(path)) return new() { Slot = slot };

        try
        {
            var file = JsonSerializer.Deserialize<SaveFile>(File.ReadAllText(path));
            return new()
            {
                Slot = slot,
                Exists = file?.State is not null,
                SavedAt = file?.SavedAt,
                CurrentMonth = file?.State.CurrentMonth,
                Money = file?.State.Stats.Money,
                UiState = file?.State.CurrentUiState
            };
        }
        catch
        {
            return new() { Slot = slot, Exists = true };
        }
    }

    private string SlotPath(int slot) => Path.Combine(_saveRoot, $"slot-{slot:00}.json");

    private static void EnsureSlot(int slot)
    {
        if (slot is < 1 or > SlotCount)
            throw new InvalidOperationException($"Save slot must be between 1 and {SlotCount}.");
    }
}
