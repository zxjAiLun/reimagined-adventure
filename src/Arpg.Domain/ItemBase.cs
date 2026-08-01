namespace Arpg.Domain;

/// <summary>
/// Small catalogue entry for the first loot slice. The full C++ catalogue is
/// intentionally deferred until the content migration stage.
/// </summary>
public sealed class ItemBaseDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public EquipmentSlot Slot { get; init; }
    public int RequiredLevel { get; init; } = 1;
    public Stats ImplicitStats { get; init; } = Stats.Neutral;
    public string? UniqueName { get; init; }
    public Stats UniqueStats { get; init; } = Stats.Neutral;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new ArgumentException("Item base id cannot be empty.", nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Item base name cannot be empty.", nameof(Name));
        }

        if (!Enum.IsDefined(Slot))
        {
            throw new ArgumentOutOfRangeException(nameof(Slot), Slot, "Unknown equipment slot.");
        }

        if (RequiredLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(RequiredLevel), "Required level must be positive.");
        }

        ArgumentNullException.ThrowIfNull(ImplicitStats);
        ImplicitStats.Validate();
        if (UniqueName != null && string.IsNullOrWhiteSpace(UniqueName))
        {
            throw new ArgumentException("Unique name cannot be blank.", nameof(UniqueName));
        }

        ArgumentNullException.ThrowIfNull(UniqueStats);
        UniqueStats.Validate();
    }
}

public static class ItemBaseLibrary
{
    private static readonly ItemBaseDefinition[] WeaponBases =
    [
        new ItemBaseDefinition
        {
            Id = "rustbound_blade",
            Name = "Rustbound Blade",
            Slot = EquipmentSlot.Weapon,
            RequiredLevel = 1,
            ImplicitStats = new Stats { DamageMultiplier = 1.10 },
            UniqueName = "Rustbound Oath",
            UniqueStats = new Stats { Armor = 4 },
        },
        new ItemBaseDefinition
        {
            Id = "hunter_bow",
            Name = "Hunter Bow",
            Slot = EquipmentSlot.Weapon,
            RequiredLevel = 1,
            ImplicitStats = new Stats { ProjectileDamageMultiplier = 1.16 },
            UniqueName = "Hunter's Vigil",
            UniqueStats = new Stats { AttackSpeedMultiplier = 1.08 },
        },
        new ItemBaseDefinition
        {
            Id = "brimstone_brand",
            Name = "Brimstone Brand",
            Slot = EquipmentSlot.Weapon,
            RequiredLevel = 1,
            ImplicitStats = new Stats
            {
                DamageMultiplier = 1.20,
                FireDamageMultiplier = 1.15,
            },
            UniqueName = "Colossus's Brand",
            UniqueStats = new Stats { MaxHp = 10 },
        },
    ];

    public static IReadOnlyList<ItemBaseDefinition> AllWeapons => WeaponBases;

    private static readonly ItemBaseDefinition[] ArmorBases =
    [
        new ItemBaseDefinition
        {
            Id = "ironhide_vest",
            Name = "Ironhide Vest",
            Slot = EquipmentSlot.Armor,
            RequiredLevel = 1,
            ImplicitStats = new Stats { Armor = 18, MaxHp = 20 },
            UniqueName = "Ironhide Aegis",
            UniqueStats = new Stats { IncomingDamageMultiplier = 0.90 },
        },
        new ItemBaseDefinition
        {
            Id = "emberweave_coat",
            Name = "Emberweave Coat",
            Slot = EquipmentSlot.Armor,
            RequiredLevel = 1,
            ImplicitStats = new Stats { FireResistance = 18, IncomingDamageMultiplier = 0.94 },
            UniqueName = "Emberweave Mantle",
            UniqueStats = new Stats { FireDamageMultiplier = 1.18 },
        },
    ];

    private static readonly ItemBaseDefinition[] RingBases =
    [
        new ItemBaseDefinition
        {
            Id = "hunters_loop",
            Name = "Hunter's Loop",
            Slot = EquipmentSlot.Ring,
            RequiredLevel = 1,
            ImplicitStats = new Stats { ProjectileDamageMultiplier = 1.10 },
            UniqueName = "Hunter's Circlet",
            UniqueStats = new Stats { AttackSpeedMultiplier = 1.12 },
        },
        new ItemBaseDefinition
        {
            Id = "ashen_band",
            Name = "Ashen Band",
            Slot = EquipmentSlot.Ring,
            RequiredLevel = 1,
            ImplicitStats = new Stats { FireDamageMultiplier = 1.10 },
            UniqueName = "Ashen Covenant",
            UniqueStats = new Stats { AreaDamageMultiplier = 1.15 },
        },
    ];

    private static readonly ItemBaseDefinition[] AmuletBases =
    [
        new ItemBaseDefinition
        {
            Id = "vanguard_charm",
            Name = "Vanguard Charm",
            Slot = EquipmentSlot.Amulet,
            RequiredLevel = 1,
            ImplicitStats = new Stats { DamageMultiplier = 1.08, MaxHp = 12 },
            UniqueName = "Vanguard's Promise",
            UniqueStats = new Stats { Armor = 8 },
        },
        new ItemBaseDefinition
        {
            Id = "echo_pendant",
            Name = "Echo Pendant",
            Slot = EquipmentSlot.Amulet,
            RequiredLevel = 1,
            ImplicitStats = new Stats { AreaDamageMultiplier = 1.10, AreaRadiusMultiplier = 1.08 },
            UniqueName = "Echo of the Deep",
            UniqueStats = new Stats { PickupRangeMultiplier = 1.20 },
        },
    ];

    private static readonly ItemBaseDefinition[] AllBases =
        WeaponBases.Concat(ArmorBases).Concat(RingBases).Concat(AmuletBases).ToArray();

    public static IReadOnlyList<ItemBaseDefinition> All => AllBases;
    public static IReadOnlyList<ItemBaseDefinition> AllArmor => ArmorBases;
    public static IReadOnlyList<ItemBaseDefinition> AllRings => RingBases;
    public static IReadOnlyList<ItemBaseDefinition> AllAmulets => AmuletBases;

    public static ItemBaseDefinition? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return AllBases.FirstOrDefault(item => item.Id == id);
    }

    public static IReadOnlyList<ItemBaseDefinition> ForSlot(EquipmentSlot slot) =>
        AllBases.Where(item => item.Slot == slot).ToArray();
}
