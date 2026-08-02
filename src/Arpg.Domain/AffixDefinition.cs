namespace Arpg.Domain;

/// <summary>
/// A candidate affix in the deterministic item roll pool. Unlike <see cref="Affix"/>,
/// this definition is not an item instance and is never mutated by a roll.
/// </summary>
public sealed record AffixDefinition(
    string Id,
    string Name,
    bool IsPrefix,
    int Tier,
    int MinimumItemLevel,
    int Weight,
    IReadOnlySet<EquipmentSlot> AllowedSlots,
    Stats Stats)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Affix definition id and name are required.");
        }

        if (Tier < 1 || MinimumItemLevel < 1 || Weight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Tier), "Affix tier, level and weight must be positive.");
        }

        if (AllowedSlots == null || AllowedSlots.Count == 0 || AllowedSlots.Any(slot => !Enum.IsDefined(slot)))
        {
            throw new ArgumentException("Affix definition must allow at least one valid equipment slot.", nameof(AllowedSlots));
        }

        ArgumentNullException.ThrowIfNull(Stats);
        Stats.Validate();
    }

    public Affix Roll() => new()
    {
        Id = Id,
        Name = Name,
        IsPrefix = IsPrefix,
        Tier = Tier,
        Stats = Stats,
    };
}

public static class AffixLibrary
{
    private static readonly IReadOnlySet<EquipmentSlot> Weapons =
        new HashSet<EquipmentSlot> { EquipmentSlot.Weapon };
    private static readonly IReadOnlySet<EquipmentSlot> Armor =
        new HashSet<EquipmentSlot> { EquipmentSlot.Armor };
    private static readonly IReadOnlySet<EquipmentSlot> Jewelry =
        new HashSet<EquipmentSlot> { EquipmentSlot.Ring, EquipmentSlot.Amulet };
    private static readonly IReadOnlySet<EquipmentSlot> AllSlots =
        new HashSet<EquipmentSlot>(Enum.GetValues<EquipmentSlot>());

    private static readonly AffixDefinition[] Definitions =
    [
        new("stalwart_t1", "Stalwart", true, 1, 1, 100, AllSlots, new Stats { MaxHp = 15 }),
        new("stalwart_t2", "Stalwart", true, 2, 4, 50, AllSlots, new Stats { MaxHp = 30 }),
        new("reinforced_t1", "Reinforced", true, 1, 1, 100, Armor, new Stats { Armor = 10 }),
        new("reinforced_t2", "Reinforced", true, 2, 4, 50, Armor, new Stats { Armor = 22 }),
        new("tempered_t1", "Tempered", true, 1, 1, 100, Weapons, new Stats { DamageMultiplier = 1.10 }),
        new("tempered_t2", "Tempered", true, 2, 4, 50, Weapons, new Stats { DamageMultiplier = 1.22 }),
        new("arcane_t1", "Arcane", true, 1, 1, 100, new HashSet<EquipmentSlot> { EquipmentSlot.Amulet }, new Stats { AreaDamageMultiplier = 1.12 }),
        new("arcane_t2", "Arcane", true, 2, 4, 50, new HashSet<EquipmentSlot> { EquipmentSlot.Amulet }, new Stats { AreaDamageMultiplier = 1.25 }),
        new("embers_t1", "of Embers", false, 1, 1, 100, AllSlots, new Stats { FireDamageMultiplier = 1.12, FireResistance = 6 }),
        new("embers_t2", "of Embers", false, 2, 4, 50, AllSlots, new Stats { FireDamageMultiplier = 1.24, FireResistance = 12 }),
        new("hunt_t1", "of the Hunt", false, 1, 1, 100, new HashSet<EquipmentSlot> { EquipmentSlot.Weapon, EquipmentSlot.Ring }, new Stats { ProjectileDamageMultiplier = 1.12 }),
        new("hunt_t2", "of the Hunt", false, 2, 4, 50, new HashSet<EquipmentSlot> { EquipmentSlot.Weapon, EquipmentSlot.Ring }, new Stats { ProjectileDamageMultiplier = 1.24 }),
        new("haste_t1", "of Haste", false, 1, 1, 100, new HashSet<EquipmentSlot> { EquipmentSlot.Weapon, EquipmentSlot.Ring }, new Stats { AttackSpeedMultiplier = 1.12 }),
        new("haste_t2", "of Haste", false, 2, 4, 50, new HashSet<EquipmentSlot> { EquipmentSlot.Weapon, EquipmentSlot.Ring }, new Stats { AttackSpeedMultiplier = 1.24 }),
        new("reach_t1", "of Reach", false, 1, 1, 100, Jewelry, new Stats { AreaRadiusMultiplier = 1.10, PickupRangeMultiplier = 1.10 }),
        new("reach_t2", "of Reach", false, 2, 4, 50, Jewelry, new Stats { AreaRadiusMultiplier = 1.22, PickupRangeMultiplier = 1.20 }),
        // Compatibility aliases for the first vertical slice's generated items.
        new("tempered_edge", "Tempered Edge", true, 1, 1, 100, Weapons, new Stats { DamageMultiplier = 1.10 }),
        new("charged_string", "Charged String", false, 1, 1, 100, Weapons, new Stats { ProjectileDamageMultiplier = 1.12 }),
        new("molten_grip", "Molten Grip", false, 1, 1, 100, Weapons, new Stats { FireDamageMultiplier = 1.14 }),
    ];

    static AffixLibrary()
    {
        foreach (var definition in Definitions)
        {
            definition.Validate();
        }
    }

    public static IReadOnlyList<AffixDefinition> All => Definitions;

    public static AffixDefinition? Find(string? id) => string.IsNullOrWhiteSpace(id)
        ? null
        : Definitions.FirstOrDefault(definition => definition.Id == id);

    public static IReadOnlyList<AffixDefinition> Candidates(
        EquipmentSlot slot,
        int itemLevel,
        bool isPrefix)
    {
        if (itemLevel < 1)
        {
            return Array.Empty<AffixDefinition>();
        }

        return Definitions
            .Where(definition => definition.IsPrefix == isPrefix
                && definition.MinimumItemLevel <= itemLevel
                && definition.AllowedSlots.Contains(slot))
            .ToArray();
    }
}
