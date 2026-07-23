namespace Arpg.Domain;

/// <summary>
/// Deterministic first-slice equipment generator. It guarantees one weapon
/// drop; drop chance, map scaling and full affix weighting come later.
/// </summary>
public sealed class LootGenerator
{
    private static readonly Affix[] WeaponAffixes =
    [
        new Affix
        {
            Id = "tempered_edge",
            Name = "Tempered Edge",
            Tier = 1,
            IsPrefix = true,
            Stats = new Stats { DamageMultiplier = 1.10 },
        },
        new Affix
        {
            Id = "charged_string",
            Name = "Charged String",
            Tier = 1,
            IsPrefix = false,
            Stats = new Stats { ProjectileDamageMultiplier = 1.12 },
        },
        new Affix
        {
            Id = "molten_grip",
            Name = "Molten Grip",
            Tier = 1,
            IsPrefix = false,
            Stats = new Stats { FireDamageMultiplier = 1.14 },
        },
    ];

    private readonly RandomService _random;
    private int _nextItemNumber;

    public LootGenerator(ulong seed = RandomService.DefaultSeed)
    {
        _random = new RandomService(seed);
    }

    public Item GenerateWeaponDrop(int itemLevel = 1, bool boss = false)
    {
        if (itemLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(itemLevel), "Item level must be positive.");
        }

        var baseIndex = boss
            ? 2
            : _random.WeightedChoiceIndex([5, 3, 2]);
        var baseDefinition = ItemBaseLibrary.AllWeapons[baseIndex];
        var affix = WeaponAffixes[_random.NextIndex(WeaponAffixes.Length)];
        var itemStats = Stats.Combine(baseDefinition.ImplicitStats, affix.Stats);
        var rarity = boss ? Rarity.Unique : (_random.Chance(35) ? Rarity.Magic : Rarity.Normal);
        var name = boss ? "Colossus's Brand" : $"{baseDefinition.Name} of {affix.Name}";

        var item = new Item
        {
            Id = $"drop_{_nextItemNumber++:D4}",
            Name = name,
            BaseId = baseDefinition.Id,
            Slot = baseDefinition.Slot,
            Rarity = rarity,
            ItemLevel = itemLevel,
            RequiredLevel = baseDefinition.RequiredLevel,
            Stats = itemStats,
            Affixes = [affix],
        };
        item.Validate();
        return item;
    }

    public Item GenerateWeaponDropForBase(
        string baseId,
        int itemLevel = 1,
        string? disallowedItemId = null)
    {
        if (itemLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(itemLevel), "Item level must be positive.");
        }

        var baseDefinition = ItemBaseLibrary.Find(baseId)
            ?? throw new ArgumentException($"Unknown weapon base '{baseId}'.", nameof(baseId));
        if (baseDefinition.Slot != EquipmentSlot.Weapon)
        {
            throw new ArgumentException($"Item base '{baseId}' is not a weapon.", nameof(baseId));
        }

        var itemId = $"drop_{_nextItemNumber++:D4}";
        while (itemId == disallowedItemId)
        {
            itemId = $"drop_{_nextItemNumber++:D4}";
        }

        var affix = WeaponAffixes[_random.NextIndex(WeaponAffixes.Length)];
        var item = new Item
        {
            Id = itemId,
            Name = $"{baseDefinition.Name} of {affix.Name}",
            BaseId = baseDefinition.Id,
            Slot = baseDefinition.Slot,
            Rarity = _random.Chance(35) ? Rarity.Magic : Rarity.Normal,
            ItemLevel = itemLevel,
            RequiredLevel = baseDefinition.RequiredLevel,
            Stats = Stats.Combine(baseDefinition.ImplicitStats, affix.Stats),
            Affixes = [affix],
        };
        item.Validate();
        return item;
    }
}
