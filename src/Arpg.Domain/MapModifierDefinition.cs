namespace Arpg.Domain;

public sealed class MapModifierDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string RiskDescription { get; init; }
    public required string RewardDescription { get; init; }
    public MapModifierStats Effects { get; init; } = new();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id)
            || string.IsNullOrWhiteSpace(Name)
            || string.IsNullOrWhiteSpace(RiskDescription)
            || string.IsNullOrWhiteSpace(RewardDescription))
        {
            throw new ArgumentException("Map modifier identity and descriptions are required.");
        }

        ArgumentNullException.ThrowIfNull(Effects);
        Effects.Validate();
    }
}

public static class MapModifierLibrary
{
    private static readonly MapModifierDefinition[] Definitions =
    [
        new MapModifierDefinition
        {
            Id = "quiet-coast",
            Name = "Quiet Coast",
            RiskDescription = "No modifier",
            RewardDescription = "Baseline map rewards",
        },
        new MapModifierDefinition
        {
            Id = "hardened-front",
            Name = "Hardened Front",
            RiskDescription = "Monsters have more life",
            RewardDescription = "Survival rewards are favored",
            Effects = new MapModifierStats
            {
                MonsterHpMultiplier = 1.15,
                ItemQuantityMultiplier = 1.05,
                BossHpMultiplier = 1.05,
            },
        },
        new MapModifierDefinition
        {
            Id = "frenzied-march",
            Name = "Frenzied March",
            RiskDescription = "Monsters move and hit harder",
            RewardDescription = "Damage rewards and item quantity increase",
            Effects = new MapModifierStats
            {
                MonsterHpMultiplier = 1.05,
                MonsterDamageBonus = 1,
                MonsterSpeedMultiplier = 1.15,
                ItemQuantityMultiplier = 1.12,
                BossHpMultiplier = 1.10,
                BossDamageMultiplier = 1.15,
            },
        },
    ];

    public static IReadOnlyList<MapModifierDefinition> All => Definitions;

    public static MapModifierDefinition? Find(string? id)
    {
        return string.IsNullOrWhiteSpace(id)
            ? null
            : Definitions.FirstOrDefault(definition => definition.Id == id);
    }
}
