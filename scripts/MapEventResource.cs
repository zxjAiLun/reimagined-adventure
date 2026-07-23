using Arpg.Domain;
using Godot;

[GlobalClass]
public partial class MapEventResource : Resource
{
    [Export] public string EventId { get; set; } = "loot_cache";
    [Export] public string DisplayName { get; set; } = "Loot Cache";
    [Export] public int EventType { get; set; } = (int)MapEventType.LootCache;
    [Export] public float Radius { get; set; } = 70.0f;
    [Export] public int ItemDropCount { get; set; } = 2;
    [Export] public float RewardMultiplier { get; set; } = 1.0f;
    [Export] public int ForgeFragmentReward { get; set; } = 1;
    [Export] public float DamageMultiplier { get; set; } = 1.0f;
    [Export] public float BuffDurationSeconds { get; set; }

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
