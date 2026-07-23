namespace Arpg.Domain;

public enum SaveRunState
{
    Playing,
    MapComplete,
}

/// <summary>
/// Persisted run state. The historical type name is retained so the first
/// vertical-slice save boundary remains source-compatible.
/// </summary>
public sealed class MinimalRunState
{
    public SaveRunState State { get; init; } = SaveRunState.Playing;
    public int MapLevel { get; init; } = 1;
    public int PlayerMaxHealth { get; init; } = 100;
    public int PlayerCurrentHealth { get; init; } = 100;
    public int ManaCharges { get; init; } = SaveSnapshot.MaxManaCharges;
    public IReadOnlyList<string> InventoryItemIds { get; init; } = Array.Empty<string>();
    public string? EquippedWeaponId { get; init; }
    public IReadOnlyList<Item> InventoryItems { get; init; } = Array.Empty<Item>();
    public Item? EquippedWeapon { get; init; }
    public IReadOnlyList<int> PassiveAllocatedIndices { get; init; } = Array.Empty<int>();
    public IReadOnlyList<string> AtlasUnlockedMapIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AtlasCompletedMapIds { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Validation-only snapshot. File I/O stays in the Godot adapter, while this
/// type owns the portable content invariants.
/// </summary>
public sealed class SaveSnapshot
{
    public const uint ExpectedMagic = 0x4D415247U;
    public const int CurrentVersion = 1;
    public const int MaxManaCharges = 3;
    public const int MaxInventoryCount = 8;

    public uint Magic { get; init; } = ExpectedMagic;
    public int Version { get; init; } = CurrentVersion;
    public SaveRunState State { get; init; } = SaveRunState.Playing;
    public int MapLevel { get; init; } = 1;
    public int PlayerMaxHealth { get; init; } = 100;
    public int PlayerCurrentHealth { get; init; } = 100;
    public int ManaCharges { get; init; } = MaxManaCharges;
    public int InventoryCount { get; init; }
    public IReadOnlyList<string> InventoryItemIds { get; init; } = Array.Empty<string>();
    public string? EquippedWeaponId { get; init; }
    public IReadOnlyList<Item> InventoryItems { get; init; } = Array.Empty<Item>();
    public Item? EquippedWeapon { get; init; }
    public IReadOnlyList<int> PassiveAllocatedIndices { get; init; } = Array.Empty<int>();
    public IReadOnlyList<string> AtlasUnlockedMapIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AtlasCompletedMapIds { get; init; } = Array.Empty<string>();
    public int SelectedNextMapOption { get; init; } = -1;
    public int SelectedMapRewardOption { get; init; } = -1;
    public bool NextMapOptionChosen { get; init; }
    public bool MapRewardChosen { get; init; }

    public static SaveSnapshot Capture(MinimalRunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var inventoryItems = state.InventoryItems?.ToArray() ?? Array.Empty<Item>();
        var itemIds = inventoryItems.Length > 0
            ? inventoryItems.Select(item => item.Id).ToArray()
            : state.InventoryItemIds?.ToArray() ?? Array.Empty<string>();
        var equippedWeapon = state.EquippedWeapon;
        var equippedWeaponId = equippedWeapon?.Id ?? state.EquippedWeaponId;
        var snapshot = new SaveSnapshot
        {
            State = state.State,
            MapLevel = state.MapLevel,
            PlayerMaxHealth = state.PlayerMaxHealth,
            PlayerCurrentHealth = state.PlayerCurrentHealth,
            ManaCharges = state.ManaCharges,
            InventoryCount = itemIds.Length,
            InventoryItemIds = itemIds,
            EquippedWeaponId = equippedWeaponId,
            InventoryItems = inventoryItems,
            EquippedWeapon = equippedWeapon,
            PassiveAllocatedIndices = state.PassiveAllocatedIndices?.ToArray() ?? Array.Empty<int>(),
            AtlasUnlockedMapIds = state.AtlasUnlockedMapIds?.ToArray() ?? Array.Empty<string>(),
            AtlasCompletedMapIds = state.AtlasCompletedMapIds?.ToArray() ?? Array.Empty<string>(),
        };
        snapshot.Validate();
        return snapshot;
    }

    public MinimalRunState Restore()
    {
        Validate();
        return new MinimalRunState
        {
            State = State,
            MapLevel = MapLevel,
            PlayerMaxHealth = PlayerMaxHealth,
            PlayerCurrentHealth = PlayerCurrentHealth,
            ManaCharges = ManaCharges,
            InventoryItemIds = InventoryItemIds.ToArray(),
            EquippedWeaponId = EquippedWeaponId,
            InventoryItems = InventoryItems.ToArray(),
            EquippedWeapon = EquippedWeapon,
            PassiveAllocatedIndices = PassiveAllocatedIndices.ToArray(),
            AtlasUnlockedMapIds = AtlasUnlockedMapIds.ToArray(),
            AtlasCompletedMapIds = AtlasCompletedMapIds.ToArray(),
        };
    }

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
            || InventoryCount < 0
            || InventoryCount > MaxInventoryCount
            || InventoryItemIds == null
            || InventoryItemIds.Count != InventoryCount
            || InventoryItemIds.Any(string.IsNullOrWhiteSpace)
            || InventoryItemIds.Distinct(StringComparer.Ordinal).Count() != InventoryItemIds.Count
            || EquippedWeaponId != null && string.IsNullOrWhiteSpace(EquippedWeaponId)
            || InventoryItems == null
            || InventoryItems.Count > MaxInventoryCount
            || InventoryItems.Any(item => item == null)
            || InventoryItems.Any(item => !IsValidItem(item))
            || InventoryItems.Count > 0
                && (!InventoryItems.Select(item => item.Id).SequenceEqual(InventoryItemIds)
                    || InventoryItems.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != InventoryItems.Count)
            || EquippedWeapon != null
                && (!IsValidItem(EquippedWeapon)
                    || EquippedWeaponId != EquippedWeapon.Id)
            || PassiveAllocatedIndices == null
            || PassiveAllocatedIndices.Any(index => index < 0)
            || PassiveAllocatedIndices.Distinct().Count() != PassiveAllocatedIndices.Count
            || AtlasUnlockedMapIds == null
            || AtlasCompletedMapIds == null
            || AtlasUnlockedMapIds.Any(string.IsNullOrWhiteSpace)
            || AtlasCompletedMapIds.Any(string.IsNullOrWhiteSpace)
            || AtlasUnlockedMapIds.Distinct(StringComparer.Ordinal).Count() != AtlasUnlockedMapIds.Count
            || AtlasCompletedMapIds.Distinct(StringComparer.Ordinal).Count() != AtlasCompletedMapIds.Count
            || !AtlasCompletedMapIds.All(AtlasUnlockedMapIds.Contains))
        {
            error = "invalid player resource values";
            return false;
        }

        if (!ValidOption(SelectedNextMapOption)
            || !ValidOption(SelectedMapRewardOption)
            || NextMapOptionChosen != (SelectedNextMapOption >= 0)
            || MapRewardChosen != (SelectedMapRewardOption >= 0)
            || NextMapOptionChosen && !MapRewardChosen
            || State == SaveRunState.Playing && (MapRewardChosen || NextMapOptionChosen))
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

    private static bool IsValidItem(Item item)
    {
        try
        {
            item.Validate();
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
