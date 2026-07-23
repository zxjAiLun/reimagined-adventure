namespace Arpg.Domain;

public sealed class Item
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string BaseId { get; init; }
    public EquipmentSlot Slot { get; init; }
    public Rarity Rarity { get; init; } = Rarity.Normal;
    public int ItemLevel { get; init; } = 1;
    public int RequiredLevel { get; init; } = 1;
    public Stats Stats { get; init; } = Stats.Neutral;
    public IReadOnlyList<Affix> Affixes { get; init; } = Array.Empty<Affix>();

    public string RarityName => Rarity.ToString();
    public string SlotName => Slot.ToString();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new ArgumentException("Item id cannot be empty.", nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Item name cannot be empty.", nameof(Name));
        }

        if (string.IsNullOrWhiteSpace(BaseId))
        {
            throw new ArgumentException("Item base id cannot be empty.", nameof(BaseId));
        }

        var baseDefinition = ItemBaseLibrary.Find(BaseId)
            ?? throw new ArgumentException($"Unknown item base '{BaseId}'.", nameof(BaseId));

        if (!Enum.IsDefined(Slot))
        {
            throw new ArgumentOutOfRangeException(nameof(Slot), Slot, "Unknown equipment slot.");
        }

        if (!Enum.IsDefined(Rarity))
        {
            throw new ArgumentOutOfRangeException(nameof(Rarity), Rarity, "Unknown item rarity.");
        }

        if (ItemLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ItemLevel), "Item level must be positive.");
        }

        if (RequiredLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(RequiredLevel), "Required level must be positive.");
        }

        if (Slot != baseDefinition.Slot)
        {
            throw new ArgumentException("Item slot does not match its base definition.", nameof(Slot));
        }

        if (RequiredLevel != baseDefinition.RequiredLevel)
        {
            throw new ArgumentException("Item required level does not match its base definition.", nameof(RequiredLevel));
        }

        ArgumentNullException.ThrowIfNull(Stats);
        Stats.Validate();
        ArgumentNullException.ThrowIfNull(Affixes);
        var affixIds = new HashSet<string>(StringComparer.Ordinal);
        var prefixCount = 0;
        var suffixCount = 0;
        var expectedStats = baseDefinition.ImplicitStats;
        foreach (var affix in Affixes)
        {
            ArgumentNullException.ThrowIfNull(affix);
            affix.Validate();
            if (!affixIds.Add(affix.Id))
            {
                throw new ArgumentException($"Item contains duplicate affix '{affix.Id}'.", nameof(Affixes));
            }

            if (affix.IsPrefix)
            {
                prefixCount++;
            }
            else
            {
                suffixCount++;
            }

            if (prefixCount > 1 || suffixCount > 1)
            {
                throw new ArgumentException("Item contains duplicate affix class.", nameof(Affixes));
            }

            expectedStats = Stats.Combine(expectedStats, affix.Stats);
        }

        if (!Stats.EquivalentTo(expectedStats))
        {
            throw new ArgumentException("Item stats do not match base implicit stats and affixes.", nameof(Stats));
        }
    }
}
