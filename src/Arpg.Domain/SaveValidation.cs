namespace Arpg.Domain;

public enum SaveRunState
{
    Playing,
    MapComplete,
}

/// <summary>
/// Validation-only snapshot. It is not a save format and intentionally does
/// not introduce file I/O or the deferred full-save system.
/// </summary>
public sealed class SaveSnapshot
{
    public const uint ExpectedMagic = 0x4D415247U;
    public const int CurrentVersion = 1;
    public const int MaxManaCharges = 3;

    public uint Magic { get; init; } = ExpectedMagic;
    public int Version { get; init; } = CurrentVersion;
    public SaveRunState State { get; init; } = SaveRunState.Playing;
    public int MapLevel { get; init; } = 1;
    public int PlayerMaxHealth { get; init; } = 100;
    public int PlayerCurrentHealth { get; init; } = 100;
    public int ManaCharges { get; init; } = MaxManaCharges;
    public int InventoryCount { get; init; }
    public int SelectedNextMapOption { get; init; } = -1;
    public int SelectedMapRewardOption { get; init; } = -1;
    public bool NextMapOptionChosen { get; init; }
    public bool MapRewardChosen { get; init; }

    public bool TryValidate(out string error)
    {
        if (Magic != ExpectedMagic)
        {
            error = "invalid save magic";
            return false;
        }

        if (Version != CurrentVersion)
        {
            error = "unsupported save version";
            return false;
        }

        if (!Enum.IsDefined(State) || MapLevel < 1)
        {
            error = "invalid run state or map level";
            return false;
        }

        if (PlayerMaxHealth < 1
            || PlayerCurrentHealth < 0
            || PlayerCurrentHealth > PlayerMaxHealth
            || ManaCharges < 0
            || ManaCharges > MaxManaCharges
            || InventoryCount < 0)
        {
            error = "invalid player resource values";
            return false;
        }

        if (!ValidOption(SelectedNextMapOption)
            || !ValidOption(SelectedMapRewardOption)
            || NextMapOptionChosen != (SelectedNextMapOption >= 0)
            || MapRewardChosen != (SelectedMapRewardOption >= 0))
        {
            error = "invalid map option selection state";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public void Validate()
    {
        if (!TryValidate(out var error))
        {
            throw new ArgumentException(error, nameof(SaveSnapshot));
        }
    }

    private static bool ValidOption(int value) => value >= -1 && value < 3;
}
