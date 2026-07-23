using Arpg.Domain;
using Godot;

[GlobalClass]
public partial class MapRewardResource : Resource
{
    [Export] public string RewardId { get; set; } = "reward";
    [Export] public string Title { get; set; } = "Reward";
    [Export] public int Type { get; set; }
    [Export] public float DamageMultiplier { get; set; } = 1.0f;
    [Export] public int MaxHpBonus { get; set; }
    [Export] public float ItemQuantityMultiplier { get; set; } = 1.0f;

    public MapRewardDefinition ToDomain()
    {
        var definition = new MapRewardDefinition
        {
            Id = RewardId,
            Title = Title,
            Type = (MapRewardType)Type,
            DamageMultiplier = DamageMultiplier,
            MaxHpBonus = MaxHpBonus,
            ItemQuantityMultiplier = ItemQuantityMultiplier,
        };
        definition.Validate();
        return definition;
    }
}
