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
        },
        new ItemBaseDefinition
        {
            Id = "hunter_bow",
            Name = "Hunter Bow",
            Slot = EquipmentSlot.Weapon,
            RequiredLevel = 1,
            ImplicitStats = new Stats { ProjectileDamageMultiplier = 1.16 },
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
        },
    ];

    public static IReadOnlyList<ItemBaseDefinition> AllWeapons => WeaponBases;

    public static ItemBaseDefinition? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return WeaponBases.FirstOrDefault(item => item.Id == id);
    }
}
