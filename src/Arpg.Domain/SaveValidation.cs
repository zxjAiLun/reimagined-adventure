namespace Arpg.Domain;

public enum SaveRunState
{
    Playing,
    GameOver,
    MapComplete,
}

/// <summary>
/// Persisted run state. The historical type name is retained so the first
/// vertical-slice save boundary remains source-compatible.
/// </summary>
public sealed class MinimalRunState
{
    public SaveRunState State { get; init; } = SaveRunState.Playing;
    public ulong RunSeed { get; init; } = RandomService.DefaultSeed;
    public int ItemSequence { get; init; }
    public int MapLevel { get; init; } = 1;
    public ulong LootRandomState { get; init; } = RandomService.DeriveSeed(RandomService.DefaultSeed, 1);
    public ulong CraftingRandomState { get; init; } = RandomService.DeriveSeed(RandomService.DefaultSeed, 2);
    public ulong EventRandomState { get; init; } = RandomService.DeriveSeed(RandomService.DefaultSeed, 3);
    public int PlayerMaxHealth { get; init; } = 100;
    public int PlayerCurrentHealth { get; init; } = 100;
    public Stats RewardStats { get; init; } = Stats.Neutral;
    public int ManaCharges { get; init; } = SaveSnapshot.MaxManaCharges;
    public IReadOnlyList<string> InventoryItemIds { get; init; } = Array.Empty<string>();
    public string? EquippedWeaponId { get; init; }
    public IReadOnlyList<Item> InventoryItems { get; init; } = Array.Empty<Item>();
    public Item? EquippedWeapon { get; init; }
    public IReadOnlyDictionary<EquipmentSlot, Item> EquippedItemsBySlot { get; init; } =
        new Dictionary<EquipmentSlot, Item>();
    public int ForgeFragments { get; init; }
    public IReadOnlyList<Item> StashItems { get; init; } = Array.Empty<Item>();
    public MapCompletePhase MapCompletePhase { get; init; } = MapCompletePhase.RewardChoice;
    public IReadOnlyList<string> UnlockedSupportIds { get; init; } = SkillLoadout.DefaultUnlockedSupportIds;
    public IReadOnlyDictionary<SkillSlot, string> SupportIdBySkillSlot { get; init; } =
        new Dictionary<SkillSlot, string>();
    public IReadOnlyList<int> PassiveAllocatedIndices { get; init; } = Array.Empty<int>();
    public IReadOnlyList<string> AtlasUnlockedMapIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AtlasCompletedMapIds { get; init; } = Array.Empty<string>();
    public string CurrentAtlasMapId { get; init; } = "quiet-coast";
    public string? PendingAtlasMapId { get; init; }
    public int RouteSelectionCount { get; init; }
    public int SelectedNextMapOption { get; init; } = -1;
    public int SelectedMapRewardOption { get; init; } = -1;
    public bool NextMapOptionChosen { get; init; }
    public bool MapRewardChosen { get; init; }
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
    public const int MaxInventoryCount = 16;

    public uint Magic { get; init; } = ExpectedMagic;
    public int Version { get; init; } = CurrentVersion;
    public SaveRunState State { get; init; } = SaveRunState.Playing;
    public ulong RunSeed { get; init; } = RandomService.DefaultSeed;
    public int ItemSequence { get; init; }
    public int MapLevel { get; init; } = 1;
    public ulong LootRandomState { get; init; } = RandomService.DeriveSeed(RandomService.DefaultSeed, 1);
    public ulong CraftingRandomState { get; init; } = RandomService.DeriveSeed(RandomService.DefaultSeed, 2);
    public ulong EventRandomState { get; init; } = RandomService.DeriveSeed(RandomService.DefaultSeed, 3);
    public int PlayerMaxHealth { get; init; } = 100;
    public int PlayerCurrentHealth { get; init; } = 100;
    public Stats RewardStats { get; init; } = Stats.Neutral;
    public int ManaCharges { get; init; } = MaxManaCharges;
    public int InventoryCount { get; init; }
    public IReadOnlyList<string> InventoryItemIds { get; init; } = Array.Empty<string>();
    public string? EquippedWeaponId { get; init; }
    public IReadOnlyList<Item> InventoryItems { get; init; } = Array.Empty<Item>();
    public Item? EquippedWeapon { get; init; }
    public IReadOnlyDictionary<EquipmentSlot, Item> EquippedItemsBySlot { get; init; } =
        new Dictionary<EquipmentSlot, Item>();
    public int ForgeFragments { get; init; }
    public IReadOnlyList<Item> StashItems { get; init; } = Array.Empty<Item>();
    public MapCompletePhase MapCompletePhase { get; init; } = MapCompletePhase.RewardChoice;
    public IReadOnlyList<string> UnlockedSupportIds { get; init; } = SkillLoadout.DefaultUnlockedSupportIds;
    public IReadOnlyDictionary<SkillSlot, string> SupportIdBySkillSlot { get; init; } =
        new Dictionary<SkillSlot, string>();
    public IReadOnlyList<int> PassiveAllocatedIndices { get; init; } = Array.Empty<int>();
    public IReadOnlyList<string> AtlasUnlockedMapIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AtlasCompletedMapIds { get; init; } = Array.Empty<string>();
    public string CurrentAtlasMapId { get; init; } = "quiet-coast";
    public string? PendingAtlasMapId { get; init; }
    public int RouteSelectionCount { get; init; }
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
        var equippedItems = state.EquippedItemsBySlot?.ToDictionary(pair => pair.Key, pair => pair.Value)
            ?? new Dictionary<EquipmentSlot, Item>();
        if (state.EquippedWeapon != null && !equippedItems.ContainsKey(EquipmentSlot.Weapon))
        {
            equippedItems[EquipmentSlot.Weapon] = state.EquippedWeapon;
        }

        var equippedWeapon = equippedItems.GetValueOrDefault(EquipmentSlot.Weapon);
        var equippedWeaponId = equippedWeapon?.Id ?? state.EquippedWeaponId;
        var mapCompletePhase = ResolveMapCompletePhase(
            state.State,
            state.MapRewardChosen,
            state.NextMapOptionChosen,
            state.PendingAtlasMapId,
            state.MapCompletePhase);
        var snapshot = new SaveSnapshot
        {
            State = state.State,
            RunSeed = state.RunSeed,
            ItemSequence = state.ItemSequence,
            MapLevel = state.MapLevel,
            LootRandomState = state.LootRandomState,
            CraftingRandomState = state.CraftingRandomState,
            EventRandomState = state.EventRandomState,
            PlayerMaxHealth = state.PlayerMaxHealth,
            PlayerCurrentHealth = state.PlayerCurrentHealth,
            RewardStats = state.RewardStats,
            ManaCharges = state.ManaCharges,
            InventoryCount = itemIds.Length,
            InventoryItemIds = itemIds,
            EquippedWeaponId = equippedWeaponId,
            InventoryItems = inventoryItems,
            EquippedWeapon = equippedWeapon,
            EquippedItemsBySlot = equippedItems,
            ForgeFragments = state.ForgeFragments,
            StashItems = state.StashItems?.ToArray() ?? Array.Empty<Item>(),
            MapCompletePhase = mapCompletePhase,
            UnlockedSupportIds = state.UnlockedSupportIds?.ToArray() ?? SkillLoadout.DefaultUnlockedSupportIds.ToArray(),
            SupportIdBySkillSlot = state.SupportIdBySkillSlot?.ToDictionary(pair => pair.Key, pair => pair.Value)
                ?? new Dictionary<SkillSlot, string>(),
            PassiveAllocatedIndices = state.PassiveAllocatedIndices?.ToArray() ?? Array.Empty<int>(),
            AtlasUnlockedMapIds = state.AtlasUnlockedMapIds?.ToArray() ?? Array.Empty<string>(),
            AtlasCompletedMapIds = state.AtlasCompletedMapIds?.ToArray() ?? Array.Empty<string>(),
            CurrentAtlasMapId = state.CurrentAtlasMapId,
            PendingAtlasMapId = state.PendingAtlasMapId,
            RouteSelectionCount = state.RouteSelectionCount,
            SelectedNextMapOption = state.SelectedNextMapOption,
            SelectedMapRewardOption = state.SelectedMapRewardOption,
            NextMapOptionChosen = state.NextMapOptionChosen,
            MapRewardChosen = state.MapRewardChosen,
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
            RunSeed = RunSeed,
            ItemSequence = ItemSequence,
            MapLevel = MapLevel,
            LootRandomState = LootRandomState,
            CraftingRandomState = CraftingRandomState,
            EventRandomState = EventRandomState,
            PlayerMaxHealth = PlayerMaxHealth,
            PlayerCurrentHealth = PlayerCurrentHealth,
            RewardStats = RewardStats,
            ManaCharges = ManaCharges,
            InventoryItemIds = InventoryItemIds.ToArray(),
            EquippedWeaponId = EquippedWeaponId,
            InventoryItems = InventoryItems.ToArray(),
            EquippedWeapon = EquippedWeapon,
            EquippedItemsBySlot = EquippedItemsBySlot.ToDictionary(pair => pair.Key, pair => pair.Value),
            ForgeFragments = ForgeFragments,
            StashItems = StashItems.ToArray(),
            MapCompletePhase = MapCompletePhase,
            UnlockedSupportIds = UnlockedSupportIds.ToArray(),
            SupportIdBySkillSlot = SupportIdBySkillSlot.ToDictionary(pair => pair.Key, pair => pair.Value),
            PassiveAllocatedIndices = PassiveAllocatedIndices.ToArray(),
            AtlasUnlockedMapIds = AtlasUnlockedMapIds.ToArray(),
            AtlasCompletedMapIds = AtlasCompletedMapIds.ToArray(),
            CurrentAtlasMapId = CurrentAtlasMapId,
            PendingAtlasMapId = PendingAtlasMapId,
            RouteSelectionCount = RouteSelectionCount,
            SelectedNextMapOption = SelectedNextMapOption,
            SelectedMapRewardOption = SelectedMapRewardOption,
            NextMapOptionChosen = NextMapOptionChosen,
            MapRewardChosen = MapRewardChosen,
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

        if (!Enum.IsDefined(State) || MapLevel < 1 || ItemSequence < 0)
        {
            error = "invalid run state or map level";
            return false;
        }

        if (PlayerMaxHealth < 1
            || PlayerCurrentHealth < 0
            || PlayerCurrentHealth > PlayerMaxHealth
            || RewardStats == null
            || !IsValidStats(RewardStats)
            || ManaCharges < 0
            || ManaCharges > MaxManaCharges
            || ForgeFragments < 0
            || !Enum.IsDefined(MapCompletePhase)
            || MapCompletePhase != MapCompletePhase.RewardChoice
                && (State != SaveRunState.MapComplete || !MapRewardChosen)
            || StashItems == null
            || StashItems.Count > 24
            || StashItems.Any(item => item == null || !IsValidItem(item))
            || StashItems.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != StashItems.Count
            || StashItems.Any(item => InventoryItemIds.Contains(item.Id, StringComparer.Ordinal)
                || EquippedItemsBySlot.Values.Any(equipped => equipped.Id == item.Id))
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
            || EquippedItemsBySlot == null
            || EquippedItemsBySlot.Any(pair => !Enum.IsDefined(pair.Key) || pair.Value == null || !IsValidItem(pair.Value))
            || EquippedItemsBySlot.Values.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count()
                != EquippedItemsBySlot.Count
            || EquippedItemsBySlot.TryGetValue(EquipmentSlot.Weapon, out var mappedWeapon)
                && (EquippedWeapon == null || mappedWeapon.Id != EquippedWeapon.Id)
            || EquippedItemsBySlot.Values.Any(item => InventoryItemIds.Contains(item.Id, StringComparer.Ordinal))
            || !IsValidSkillLoadout(UnlockedSupportIds, SupportIdBySkillSlot)
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

        if (string.IsNullOrWhiteSpace(CurrentAtlasMapId)
            || PendingAtlasMapId == CurrentAtlasMapId
            || RouteSelectionCount < 0)
        {
            error = "invalid atlas route state";
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

    public static MapCompletePhase ResolveMapCompletePhase(
        SaveRunState state,
        bool mapRewardChosen,
        bool nextMapOptionChosen,
        string? pendingAtlasMapId,
        MapCompletePhase phase)
    {
        if (state != SaveRunState.MapComplete
            || !mapRewardChosen
            || phase != MapCompletePhase.RewardChoice)
        {
            return phase;
        }

        return nextMapOptionChosen || !string.IsNullOrWhiteSpace(pendingAtlasMapId)
            ? MapCompletePhase.RouteChoice
            : MapCompletePhase.BuildManagement;
    }

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

    private static bool IsValidStats(Stats stats)
    {
        try
        {
            stats.Validate();
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsValidSkillLoadout(
        IReadOnlyList<string> unlockedSupportIds,
        IReadOnlyDictionary<SkillSlot, string> supportIdBySkillSlot)
    {
        if (unlockedSupportIds == null
            || supportIdBySkillSlot == null
            || unlockedSupportIds.Any(string.IsNullOrWhiteSpace)
            || unlockedSupportIds.Distinct(StringComparer.Ordinal).Count() != unlockedSupportIds.Count
            || unlockedSupportIds.Any(supportId => SupportLibrary.Find(supportId) == null))
        {
            return false;
        }

        try
        {
            var loadout = new SkillLoadout(SkillLibrary.DefaultBar(), unlockedSupportIds);
            foreach (var pair in supportIdBySkillSlot)
            {
                if (!Enum.IsDefined(pair.Key) || !loadout.TryAttach(pair.Key, pair.Value))
                {
                    return false;
                }
            }

            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
