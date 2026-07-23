using Arpg.Domain;
using Godot;

[GlobalClass]
public partial class BossDefinitionResource : Resource
{
    [Export] public string BossId { get; set; } = "brimstone_colossus";
    [Export] public string DisplayName { get; set; } = "Brimstone Colossus";
    [Export] public int MaxHealth { get; set; } = 160;
    [Export] public int FireResistance { get; set; } = 25;
    [Export] public double MoveSpeed { get; set; } = 55.0;
    [Export] public double RecoverySeconds { get; set; } = 0.65;

    [Export] public int MagmaSlamDamage { get; set; } = 14;
    [Export] public double MagmaSlamPreparationSeconds { get; set; } = 0.75;
    [Export] public double MagmaSlamRadius { get; set; } = 130.0;
    [Export] public double MagmaSlamRange { get; set; } = 165.0;

    [Export] public int FlameSpearDamage { get; set; } = 10;
    [Export] public double FlameSpearPreparationSeconds { get; set; } = 0.55;
    [Export] public double FlameSpearRange { get; set; } = 520.0;

    public BossDefinition ToDomain()
    {
        var definition = new BossDefinition
        {
            Id = BossId,
            Name = DisplayName,
            MaxHealth = MaxHealth,
            FireResistance = FireResistance,
            MoveSpeed = MoveSpeed,
            RecoverySeconds = RecoverySeconds,
            Attacks =
            [
                new BossAttackDefinition
                {
                    Id = "magma_slam",
                    Name = "Magma Slam",
                    Kind = BossAttackKind.MagmaSlam,
                    DamageType = DamageType.Fire,
                    Damage = MagmaSlamDamage,
                    PreparationSeconds = MagmaSlamPreparationSeconds,
                    Radius = MagmaSlamRadius,
                    Range = MagmaSlamRange,
                },
                new BossAttackDefinition
                {
                    Id = "flame_spear",
                    Name = "Flame Spear",
                    Kind = BossAttackKind.FlameSpear,
                    DamageType = DamageType.Fire,
                    Damage = FlameSpearDamage,
                    PreparationSeconds = FlameSpearPreparationSeconds,
                    Range = FlameSpearRange,
                },
            ],
        };
        definition.Validate();
        return definition;
    }
}
