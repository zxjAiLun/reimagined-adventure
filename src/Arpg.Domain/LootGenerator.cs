namespace Arpg.Domain;

public enum LootSourceKind
{
    Feral,
    Spitter,
    Elite,
    Boss,
    Reward,
    Chest,
    Crafting,
}

/// <summary>
/// Complete deterministic input to one item roll. Map and encounter identity
/// are carried for auditability and future source-specific pools; they never
/// create another random stream.
/// </summary>
public sealed record ItemRollContext(
    int ItemLevel,
    LootSourceKind Source,
    EquipmentSlot? ForcedSlot = null,
    double RarityMultiplier = 1.0,
    bool GuaranteedUnique = false,
    string AtlasMapId = "",
    string EncounterId = "",
    Rarity? ForcedRarity = null,
    string? ForcedBaseId = null)
{
    public void Validate()
    {
        if (ItemLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ItemLevel), "Item level must be positive.");
        }

        if (!Enum.IsDefined(Source))
        {
            throw new ArgumentOutOfRangeException(nameof(Source), Source, "Unknown loot source.");
        }

        if (ForcedSlot.HasValue && !Enum.IsDefined(ForcedSlot.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(ForcedSlot), ForcedSlot, "Unknown forced equipment slot.");
        }

        if (!double.IsFinite(RarityMultiplier) || RarityMultiplier < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(RarityMultiplier), "Rarity multiplier must be finite and non-negative.");
        }

        if (ForcedRarity.HasValue && !Enum.IsDefined(ForcedRarity.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(ForcedRarity), ForcedRarity, "Unknown forced rarity.");
        }

        if (ForcedBaseId != null && string.IsNullOrWhiteSpace(ForcedBaseId))
        {
            throw new ArgumentException("Forced base id cannot be blank.", nameof(ForcedBaseId));
        }
    }
}

/// <summary>
/// Deterministic item generator. Every roll consumes only the supplied random
/// stream and the shared item identity source; no Godot or runtime RNG is used.
/// </summary>
public sealed class LootGenerator
{
    private readonly RandomService _random;
    private readonly IItemIdSource _itemIdSource;
    private int _nextItemNumber;

    public LootGenerator(ulong seed = RandomService.DefaultSeed, IItemIdSource? itemIdSource = null)
    {
        _random = new RandomService(seed);
        _itemIdSource = itemIdSource ?? new LocalItemIdSource();
    }

    public LootGenerator(RandomService random, IItemIdSource itemIdSource)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _itemIdSource = itemIdSource ?? throw new ArgumentNullException(nameof(itemIdSource));
    }

    public Item GenerateItemDrop(ItemRollContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Validate();

        var baseDefinition = ResolveBase(context);
        var rarity = RollRarity(context);
        var affixes = RollAffixes(baseDefinition.Slot, context.ItemLevel, rarity);
        var itemStats = baseDefinition.ImplicitStats;
        if (rarity == Rarity.Unique)
        {
            itemStats = Stats.Combine(itemStats, baseDefinition.UniqueStats);
        }

        foreach (var affix in affixes)
        {
            itemStats = Stats.Combine(itemStats, affix.Stats);
        }

        var item = new Item
        {
            Id = NextItemId(),
            Name = BuildName(baseDefinition, rarity, affixes),
            BaseId = baseDefinition.Id,
            Slot = baseDefinition.Slot,
            Rarity = rarity,
            ItemLevel = context.ItemLevel,
            RequiredLevel = baseDefinition.RequiredLevel,
            Stats = itemStats,
            Affixes = affixes,
        };
        item.Validate();
        return item;
    }

    /// <summary>
    /// Compatibility API for the original vertical slice. It intentionally
    /// produces a Magic weapon so old callers that expect one affix remain
    /// valid while new content uses <see cref="GenerateItemDrop"/>.
    /// </summary>
    public Item GenerateWeaponDrop(int itemLevel = 1, bool boss = false)
    {
        return GenerateItemDrop(new ItemRollContext(
            itemLevel,
            boss ? LootSourceKind.Boss : LootSourceKind.Feral,
            EquipmentSlot.Weapon,
            RarityMultiplier: 1.0,
            GuaranteedUnique: boss,
            ForcedRarity: boss ? null : Rarity.Magic,
            ForcedBaseId: boss ? "brimstone_brand" : null));
    }

    public Item GenerateWeaponDropForBase(
        string baseId,
        int itemLevel = 1,
        string? disallowedItemId = null)
    {
        var item = GenerateItemDrop(new ItemRollContext(
            itemLevel,
            LootSourceKind.Crafting,
            EquipmentSlot.Weapon,
            ForcedRarity: Rarity.Magic,
            ForcedBaseId: baseId));
        while (item.Id == disallowedItemId)
        {
            item = GenerateItemDrop(new ItemRollContext(
                itemLevel,
                LootSourceKind.Crafting,
                EquipmentSlot.Weapon,
                ForcedRarity: Rarity.Magic,
                ForcedBaseId: baseId));
        }

        return item;
    }

    public LootDropResult GenerateDrops(
        ItemRollContext context,
        LootDropProfile profile,
        MapModifierStats? modifier = null,
        Stats? rewardStats = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        context.Validate();
        modifier ??= new MapModifierStats();
        modifier.Validate();
        rewardStats ??= Stats.Neutral;
        rewardStats.Validate();

        var quantityMultiplier = modifier.ItemQuantityMultiplier * rewardStats.ItemQuantityMultiplier;
        var rarityMultiplier = context.RarityMultiplier * modifier.ItemRarityMultiplier;
        if (!double.IsFinite(quantityMultiplier) || quantityMultiplier < 0.0
            || !double.IsFinite(rarityMultiplier) || rarityMultiplier < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(modifier), "Loot multipliers must be finite and non-negative.");
        }

        var itemLevelLong = (long)context.ItemLevel + modifier.ItemLevelBonus;
        var itemLevel = itemLevelLong <= 1
            ? 1
            : itemLevelLong >= int.MaxValue ? int.MaxValue : (int)itemLevelLong;
        var attempts = profile.MaximumItemDrops == 0
            ? 0
            : Math.Clamp((int)Math.Ceiling(profile.MaximumItemDrops * Math.Max(1.0, quantityMultiplier)), 1, profile.MaximumItemDrops);
        var chance = Math.Clamp((int)Math.Round(profile.BaseDropChancePercent * quantityMultiplier), 0, 100);
        var items = new List<Item>();
        for (var index = 0; index < attempts; index++)
        {
            if (profile.GuaranteedItem && index == 0 || _random.Chance(chance))
            {
                items.Add(GenerateItemDrop(context with
                {
                    ItemLevel = itemLevel,
                    RarityMultiplier = rarityMultiplier,
                }));
            }
        }

        var fragments = profile.ForgeFragmentAmount > 0
            && _random.Chance(Math.Clamp(
                (int)Math.Round(profile.ForgeFragmentChancePercent * quantityMultiplier),
                0,
                100))
            ? profile.ForgeFragmentAmount
            : 0;
        return new LootDropResult(items, fragments);
    }

    private ItemBaseDefinition ResolveBase(ItemRollContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.ForcedBaseId))
        {
            var forced = ItemBaseLibrary.Find(context.ForcedBaseId)
                ?? throw new ArgumentException($"Unknown forced item base '{context.ForcedBaseId}'.", nameof(context));
            if (context.ForcedSlot.HasValue && forced.Slot != context.ForcedSlot.Value)
            {
                throw new ArgumentException("Forced base and forced slot do not match.", nameof(context));
            }

            return forced;
        }

        var candidates = context.ForcedSlot.HasValue
            ? ItemBaseLibrary.ForSlot(context.ForcedSlot.Value)
            : ItemBaseLibrary.All;
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No item base is available for the requested slot.");
        }

        return candidates[_random.NextIndex(candidates.Count)];
    }

    private Rarity RollRarity(ItemRollContext context)
    {
        if (context.GuaranteedUnique)
        {
            return Rarity.Unique;
        }

        if (context.ForcedRarity.HasValue)
        {
            return context.ForcedRarity.Value;
        }

        var minimum = context.Source switch
        {
            LootSourceKind.Boss => Rarity.Rare,
            LootSourceKind.Elite => Rarity.Magic,
            _ => Rarity.Normal,
        };
        var normalWeight = minimum > Rarity.Normal ? 0 : 60;
        var magicWeight = minimum > Rarity.Magic ? 0 : Math.Max(0, (int)Math.Round(30.0 * context.RarityMultiplier));
        var rareWeight = Math.Max(1, (int)Math.Round(10.0 * Math.Max(1.0, context.RarityMultiplier)));
        var choice = _random.WeightedChoiceIndex([normalWeight, magicWeight, rareWeight]);
        return choice switch
        {
            0 => Rarity.Normal,
            1 => Rarity.Magic,
            _ => Rarity.Rare,
        };
    }

    private IReadOnlyList<Affix> RollAffixes(EquipmentSlot slot, int itemLevel, Rarity rarity)
    {
        if (rarity is Rarity.Normal or Rarity.Unique)
        {
            return Array.Empty<Affix>();
        }

        var prefixes = AffixLibrary.Candidates(slot, itemLevel, isPrefix: true);
        var suffixes = AffixLibrary.Candidates(slot, itemLevel, isPrefix: false);
        if (rarity == Rarity.Magic)
        {
            var candidates = prefixes.Concat(suffixes).ToArray();
            return candidates.Length == 0
                ? Array.Empty<Affix>()
                : [Choose(candidates).Roll()];
        }

        if (prefixes.Count == 0 || suffixes.Count == 0)
        {
            throw new InvalidOperationException($"No complete rare affix pool is available for slot {slot}.");
        }

        return [Choose(prefixes).Roll(), Choose(suffixes).Roll()];
    }

    private AffixDefinition Choose(IReadOnlyList<AffixDefinition> candidates)
    {
        var weights = candidates.Select(candidate => candidate.Weight).ToArray();
        return candidates[_random.WeightedChoiceIndex(weights)];
    }

    private static string BuildName(
        ItemBaseDefinition baseDefinition,
        Rarity rarity,
        IReadOnlyList<Affix> affixes)
    {
        if (rarity == Rarity.Unique)
        {
            return baseDefinition.UniqueName ?? baseDefinition.Name;
        }

        if (affixes.Count == 0)
        {
            return baseDefinition.Name;
        }

        var prefix = affixes.FirstOrDefault(affix => affix.IsPrefix);
        var suffix = affixes.FirstOrDefault(affix => !affix.IsPrefix);
        return prefix != null && suffix != null
            ? $"{prefix.Name} {baseDefinition.Name} {suffix.Name}"
            : prefix != null
                ? $"{prefix.Name} {baseDefinition.Name}"
                : $"{baseDefinition.Name} {suffix!.Name}";
    }

    private string NextItemId()
    {
        if (_itemIdSource is LocalItemIdSource)
        {
            return $"drop_{_nextItemNumber++:D4}";
        }

        return _itemIdSource.NextId();
    }

    private sealed class LocalItemIdSource : IItemIdSource
    {
        public int ItemSequence => 0;

        public string NextId() => throw new InvalidOperationException("Local item id source is generator-owned.");
    }
}
