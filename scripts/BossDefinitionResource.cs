using Arpg.Domain;
using Godot;

[GlobalClass]
public partial class BossDefinitionResource : Resource
{
    [Export] public string BossId { get; set; } = "brimstone_colossus";
    [Export] public string DisplayName { get; set; } = "Brimstone Colossus";
    [Export] public int MaxHealth { get; set; } = 160;
    [Export] public int FireResistance { get; set; } = 25;
    [Export] public float MoveSpeed { get; set; } = 55.0f;
    [Export] public float RecoverySeconds { get; set; } = 0.65f;

    [Export] public int MagmaSlamDamage { get; set; } = 14;
    [Export] public float MagmaSlamPreparationSeconds { get; set; } = 0.75f;
    [Export] public float MagmaSlamRadius { get; set; } = 130.0f;
    [Export] public float MagmaSlamRange { get; set; } = 165.0f;

    [Export] public int FlameSpearDamage { get; set; } = 10;
    [Export] public float FlameSpearPreparationSeconds { get; set; } = 0.55f;
    [Export] public float FlameSpearRange { get; set; } = 520.0f;

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
                new BossAttackDefinition
                {
                    Id = "molten_ring",
                    Name = "Molten Ring",
                    Kind = BossAttackKind.MoltenRing,
                    DamageType = DamageType.Fire,
                    Damage = 12,
                    PreparationSeconds = 0.80,
                    Radius = 4.5,
                },
                new BossAttackDefinition
                {
                    Id = "ember_barrage",
                    Name = "Ember Barrage",
                    Kind = BossAttackKind.EmberBarrage,
                    DamageType = DamageType.Fire,
                    Damage = 8,
                    PreparationSeconds = 0.35,
                    Range = 8.0,
                },
            ],
            Phases = BossLibrary.BrimstoneColossus().Phases,
        };
        definition.Validate();
        return definition;
    }
}
