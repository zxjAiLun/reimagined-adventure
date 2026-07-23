namespace Arpg.Domain;

public enum MapRewardType
{
    Damage,
    MaxHp,
    ItemQuantity,
}

public sealed class MapRewardDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public MapRewardType Type { get; init; }
    public double DamageMultiplier { get; init; } = 1.0;
    public int MaxHpBonus { get; init; }
    public double ItemQuantityMultiplier { get; init; } = 1.0;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Title))
        {
            throw new ArgumentException("Map reward id and title are required.");
        }

        if (!Enum.IsDefined(Type)
            || !double.IsFinite(DamageMultiplier)
            || DamageMultiplier < 0.0
            || MaxHpBonus < 0
            || !double.IsFinite(ItemQuantityMultiplier)
            || ItemQuantityMultiplier < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(Type), "Map reward values are out of range.");
        }
    }

    public Stats Apply(Stats current)
    {
        ArgumentNullException.ThrowIfNull(current);
        Validate();
        return Stats.Combine(current, new Stats
        {
            DamageMultiplier = DamageMultiplier,
            MaxHp = MaxHpBonus,
            ItemQuantityMultiplier = ItemQuantityMultiplier,
        });
    }
}

public static class MapRewardLibrary
{
    public static IReadOnlyList<MapRewardDefinition> FallbackRewards =>
    [
        new MapRewardDefinition
        {
            Id = "global_damage",
            Title = "+20% Global Damage",
            Type = MapRewardType.Damage,
            DamageMultiplier = 1.20,
        },
        new MapRewardDefinition
        {
            Id = "max_hp",
            Title = "+1 Max HP",
            Type = MapRewardType.MaxHp,
            MaxHpBonus = 1,
        },
        new MapRewardDefinition
        {
            Id = "item_quantity",
            Title = "+15% Future Item Quantity",
            Type = MapRewardType.ItemQuantity,
            ItemQuantityMultiplier = 1.15,
        },
    ];
}
