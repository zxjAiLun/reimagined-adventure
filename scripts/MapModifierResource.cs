using Arpg.Domain;
using Godot;

[GlobalClass]
public partial class MapModifierResource : Resource
{
    [Export] public string ModifierId { get; set; } = "quiet-coast";
    [Export] public string DisplayName { get; set; } = "Quiet Coast";
    [Export] public string RiskDescription { get; set; } = "No modifier";
    [Export] public string RewardDescription { get; set; } = "Baseline map rewards";
    [Export] public double MonsterHpMultiplier { get; set; } = 1.0;
    [Export] public int MonsterDamageBonus { get; set; }
    [Export] public double MonsterSpeedMultiplier { get; set; } = 1.0;
    [Export] public double ItemQuantityMultiplier { get; set; } = 1.0;
    [Export] public int BossDropBonus { get; set; }
    [Export] public double BossHpMultiplier { get; set; } = 1.0;
    [Export] public double BossDamageMultiplier { get; set; } = 1.0;
    [Export] public int ItemLevelBonus { get; set; }
    [Export] public double EventRewardMultiplier { get; set; } = 1.0;
    [Export] public double ItemRarityMultiplier { get; set; } = 1.0;

    public MapModifierDefinition ToDomain()
    {
        var definition = new MapModifierDefinition
        {
            Id = ModifierId,
            Name = DisplayName,
            RiskDescription = RiskDescription,
            RewardDescription = RewardDescription,
            Effects = new MapModifierStats
            {
                MonsterHpMultiplier = MonsterHpMultiplier,
                MonsterDamageBonus = MonsterDamageBonus,
                MonsterSpeedMultiplier = MonsterSpeedMultiplier,
                ItemQuantityMultiplier = ItemQuantityMultiplier,
                BossDropBonus = BossDropBonus,
                BossHpMultiplier = BossHpMultiplier,
                BossDamageMultiplier = BossDamageMultiplier,
                ItemLevelBonus = ItemLevelBonus,
                EventRewardMultiplier = EventRewardMultiplier,
                ItemRarityMultiplier = ItemRarityMultiplier,
            },
        };
        definition.Validate();
        return definition;
    }
}
