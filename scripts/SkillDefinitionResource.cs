using Arpg.Domain;
using Godot;

/// <summary>
/// Godot Resource adapter for one skill. The domain object remains the
/// authority for validation and combat rules.
/// </summary>
[GlobalClass]
public partial class SkillDefinitionResource : Resource
{
    [Export] public string SkillId { get; set; } = "skill";
    [Export] public string DisplayName { get; set; } = "Skill";
    [Export] public int Slot { get; set; }
    [Export] public int CastType { get; set; }
    [Export] public int Delivery { get; set; }
    [Export] public float CooldownSeconds { get; set; }
    [Export] public float Radius { get; set; }
    [Export] public int BaseDamage { get; set; }
    [Export] public float EffectDurationSeconds { get; set; }
    [Export] public int ProjectileCount { get; set; } = 1;
    [Export] public float SpreadAngleDegrees { get; set; }
    [Export] public float CastDelaySeconds { get; set; }
    [Export] public float DashDistance { get; set; }
    [Export] public float ManaCost { get; set; }
    [Export] public int DamageType { get; set; }

    public SkillDefinition ToDomain()
    {
        var definition = new SkillDefinition
        {
            Id = SkillId,
            Name = DisplayName,
            Slot = (SkillSlot)Slot,
            CastType = (SkillCastType)CastType,
            Delivery = (SkillDeliveryType)Delivery,
            CooldownSeconds = CooldownSeconds,
            Radius = Radius,
            BaseDamage = BaseDamage,
            EffectDurationSeconds = EffectDurationSeconds,
            ProjectileCount = ProjectileCount,
            SpreadAngleDegrees = SpreadAngleDegrees,
            CastDelaySeconds = CastDelaySeconds,
            DashDistance = DashDistance,
            ManaCost = ManaCost,
            DamageType = (DamageType)DamageType,
        };
        definition.Validate();
        return definition;
    }
}
