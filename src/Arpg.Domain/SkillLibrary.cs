namespace Arpg.Domain;

public static class SkillLibrary
{
    public static SkillDefinition SpreadShot() => new()
    {
        Id = "spread_shot",
        Name = "Spread Shot",
        Slot = SkillSlot.Primary,
        CastType = SkillCastType.Projectile,
        CooldownSeconds = 0.35,
        BaseDamage = 1,
        ProjectileCount = 3,
        SpreadAngleDegrees = 30.0,
        ManaCost = 2.0,
        DamageType = DamageType.Physical,
    };

    public static SkillDefinition Meteor() => new()
    {
        Id = "meteor",
        Name = "Meteor",
        Slot = SkillSlot.Secondary,
        CastType = SkillCastType.MouseTargetedArea,
        Delivery = SkillDeliveryType.DelayedArea,
        CooldownSeconds = 1.4,
        Radius = 110.0,
        BaseDamage = 4,
        EffectDurationSeconds = 0.30,
        CastDelaySeconds = 0.55,
        ManaCost = 12.0,
        DamageType = DamageType.Fire,
    };

    public static SkillDefinition Pulse() => new()
    {
        Id = "pulse",
        Name = "Pulse",
        Slot = SkillSlot.Utility,
        CastType = SkillCastType.SelfCenteredArea,
        CooldownSeconds = 2.0,
        Radius = 160.0,
        BaseDamage = 3,
        EffectDurationSeconds = 0.30,
        ManaCost = 12.0,
        DamageType = DamageType.Lightning,
    };

    public static SkillDefinition Dash() => new()
    {
        Id = "dash",
        Name = "Dash",
        Slot = SkillSlot.Movement,
        CastType = SkillCastType.Dash,
        CooldownSeconds = 0.8,
        DashDistance = 120.0,
        DamageType = DamageType.Physical,
    };

    public static SkillBar DefaultBar() => new(
        SpreadShot(),
        Meteor(),
        Pulse(),
        Dash());
}
