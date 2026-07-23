using Arpg.Domain;
using Godot;

[GlobalClass]
public partial class MapEventResource : Resource
{
    [Export] public string EventId { get; set; } = "loot_cache";
    [Export] public string DisplayName { get; set; } = "Loot Cache";
    [Export] public int EventType { get; set; } = (int)MapEventType.LootCache;
    [Export] public double Radius { get; set; } = 70.0;
    [Export] public int ItemDropCount { get; set; } = 2;
    [Export] public double RewardMultiplier { get; set; } = 1.0;
    [Export] public int ForgeFragmentReward { get; set; } = 1;
    [Export] public double DamageMultiplier { get; set; } = 1.0;
    [Export] public double BuffDurationSeconds { get; set; }

    public MapEventDefinition ToDomain()
    {
        var definition = new MapEventDefinition
        {
            Id = EventId,
            Name = DisplayName,
            Type = (MapEventType)EventType,
            Radius = Radius,
            ItemDropCount = ItemDropCount,
            RewardMultiplier = RewardMultiplier,
            ForgeFragmentReward = ForgeFragmentReward,
            DamageMultiplier = DamageMultiplier,
            BuffDurationSeconds = BuffDurationSeconds,
        };
        definition.Validate();
        return definition;
    }
}
