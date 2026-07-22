namespace Arpg.Domain;

public enum SkillSlot
{
    Primary,
    Secondary,
    Utility,
    Movement,
}

public enum SkillCastType
{
    Projectile,
    SelfCenteredArea,
    MouseTargetedArea,
    Dash,
}

public enum SkillDeliveryType
{
    Instant,
    DelayedArea,
}

/// <summary>
/// The gameplay contract for one active skill. Presentation and collision
/// behavior stay in the Godot layer; these values remain testable in .NET.
/// </summary>
public sealed class SkillDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public SkillSlot Slot { get; init; }
    public SkillCastType CastType { get; init; }
    public SkillDeliveryType Delivery { get; init; } = SkillDeliveryType.Instant;
    public double CooldownSeconds { get; init; }
    public double Radius { get; init; }
    public int BaseDamage { get; init; }
    public double EffectDurationSeconds { get; init; }
    public int ProjectileCount { get; init; } = 1;
    public double SpreadAngleDegrees { get; init; }
    public double CastDelaySeconds { get; init; }
    public double DashDistance { get; init; }
    public double ManaCost { get; init; }
    public DamageType DamageType { get; init; } = DamageType.Physical;

    public bool IsArea => CastType is SkillCastType.SelfCenteredArea or SkillCastType.MouseTargetedArea;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new ArgumentException("Skill id cannot be empty.", nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Skill name cannot be empty.", nameof(Name));
        }

        ValidateEnum(Slot, nameof(Slot));
        ValidateEnum(CastType, nameof(CastType));
        ValidateEnum(Delivery, nameof(Delivery));
        ValidateEnum(DamageType, nameof(DamageType));
        ValidateNonNegative(nameof(CooldownSeconds), CooldownSeconds);
        ValidateNonNegative(nameof(Radius), Radius);
        ValidateNonNegative(nameof(EffectDurationSeconds), EffectDurationSeconds);
        ValidateNonNegative(nameof(SpreadAngleDegrees), SpreadAngleDegrees);
        ValidateNonNegative(nameof(CastDelaySeconds), CastDelaySeconds);
        ValidateNonNegative(nameof(DashDistance), DashDistance);
        ValidateNonNegative(nameof(ManaCost), ManaCost);

        if (BaseDamage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BaseDamage), "Base damage cannot be negative.");
        }

        if (ProjectileCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ProjectileCount), "Projectile count must be positive.");
        }

        if (CastType == SkillCastType.Projectile && Slot != SkillSlot.Primary)
        {
            throw new ArgumentException("Projectile skills belong in the Primary slot.", nameof(Slot));
        }

        if (CastType == SkillCastType.Dash && Slot != SkillSlot.Movement)
        {
            throw new ArgumentException("Dash skills belong in the Movement slot.", nameof(Slot));
        }

        if (CastType == SkillCastType.Dash && DashDistance <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(DashDistance), "Dash distance must be positive.");
        }

        if (CastType != SkillCastType.Projectile && ProjectileCount != 1)
        {
            throw new ArgumentException("Only projectile skills can have multiple projectiles.", nameof(ProjectileCount));
        }
    }

    private static void ValidateEnum<TEnum>(TEnum value, string name)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(name, value, "Unknown enum value.");
        }
    }

    private static void ValidateNonNegative(string name, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Value must be finite and non-negative.");
        }
    }
}
