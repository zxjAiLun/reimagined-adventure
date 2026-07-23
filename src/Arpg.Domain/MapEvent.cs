namespace Arpg.Domain;

public enum MapEventType
{
    LootCache,
    Shrine,
}

public sealed class MapEventDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public MapEventType Type { get; init; }
    public double Radius { get; init; } = 70.0;
    public int ItemDropCount { get; init; }
    public double RewardMultiplier { get; init; } = 1.0;
    public int ForgeFragmentReward { get; init; }
    public double DamageMultiplier { get; init; } = 1.0;
    public double BuffDurationSeconds { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Map event id and name are required.");
        }

        if (!Enum.IsDefined(Type)
            || !double.IsFinite(Radius)
            || Radius <= 0.0
            || ItemDropCount < 0
            || !double.IsFinite(RewardMultiplier)
            || RewardMultiplier < 0.0
            || ForgeFragmentReward < 0
            || !double.IsFinite(DamageMultiplier)
            || DamageMultiplier < 0.0
            || !double.IsFinite(BuffDurationSeconds)
            || BuffDurationSeconds < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(Radius), "Map event values are out of range.");
        }

        if (Type == MapEventType.LootCache && ItemDropCount < 1)
        {
            throw new ArgumentException("Loot cache must produce at least one item.", nameof(ItemDropCount));
        }

        if (Type == MapEventType.Shrine && BuffDurationSeconds <= 0.0)
        {
            throw new ArgumentException("Shrine must have a positive buff duration.", nameof(BuffDurationSeconds));
        }
    }
}

public sealed record MapEventActivation(
    MapEventType Type,
    int ItemDropCount,
    double RewardMultiplier,
    double DamageMultiplier,
    double BuffDurationSeconds,
    int ForgeFragmentReward);

public sealed class MapEventState
{
    public bool Triggered { get; private set; }
    public bool Completed { get; private set; }
    public bool CanActivate => !Triggered && !Completed;

    public bool TryActivate(MapEventDefinition definition, out MapEventActivation? activation)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        if (!CanActivate)
        {
            activation = null;
            return false;
        }

        Triggered = true;
        Completed = true;
        activation = new MapEventActivation(
            definition.Type,
            definition.ItemDropCount,
            definition.RewardMultiplier,
            definition.DamageMultiplier,
            definition.BuffDurationSeconds,
            definition.ForgeFragmentReward);
        return true;
    }
}

public static class MapEventLibrary
{
    public static MapEventDefinition LootCache() => new()
    {
        Id = "loot_cache",
        Name = "Loot Cache",
        Type = MapEventType.LootCache,
        Radius = 70.0,
        ItemDropCount = 2,
        RewardMultiplier = 1.0,
        ForgeFragmentReward = 1,
    };

    public static MapEventDefinition Shrine() => new()
    {
        Id = "shrine",
        Name = "Shrine",
        Type = MapEventType.Shrine,
        Radius = 70.0,
        DamageMultiplier = 1.35,
        BuffDurationSeconds = 20.0,
        ForgeFragmentReward = 1,
    };
}
