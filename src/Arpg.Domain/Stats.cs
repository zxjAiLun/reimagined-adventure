namespace Arpg.Domain;

/// <summary>
/// Neutral character modifiers used by the first combat slice.
///
/// Integer values aggregate additively, while multipliers aggregate
/// multiplicatively. More specialized resource and ailment fields can be
/// added later without making the Godot layer the owner of these rules.
/// </summary>
public sealed class Stats
{
    public int MaxHp { get; init; }
    public double MoveSpeedMultiplier { get; init; } = 1.0;
    public double DamageMultiplier { get; init; } = 1.0;
    public double AttackSpeedMultiplier { get; init; } = 1.0;
    public double CooldownRecoveryMultiplier { get; init; } = 1.0;
    public double PickupRangeMultiplier { get; init; } = 1.0;
    public double ProjectileDamageMultiplier { get; init; } = 1.0;
    public double AreaDamageMultiplier { get; init; } = 1.0;
    public double AreaRadiusMultiplier { get; init; } = 1.0;
    public int Armor { get; init; }
    public int ProjectileCountBonus { get; init; }
    public int AilmentChanceBonus { get; init; }
    public double AilmentDurationMultiplier { get; init; } = 1.0;
    public double DamageOverTimeMultiplier { get; init; } = 1.0;
    public double LifeFlaskEffectMultiplier { get; init; } = 1.0;
    public double ItemQuantityMultiplier { get; init; } = 1.0;
    public double IncomingDamageMultiplier { get; init; } = 1.0;

    public double PhysicalDamageMultiplier { get; init; } = 1.0;
    public double FireDamageMultiplier { get; init; } = 1.0;
    public double ColdDamageMultiplier { get; init; } = 1.0;
    public double LightningDamageMultiplier { get; init; } = 1.0;
    public double PoisonDamageMultiplier { get; init; } = 1.0;

    public int FireResistance { get; init; }
    public int ColdResistance { get; init; }
    public int LightningResistance { get; init; }
    public int PoisonResistance { get; init; }

    public static Stats Neutral => new();

    public void Validate()
    {
        if (MaxHp < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxHp), "Max HP cannot be negative.");
        }

        if (Armor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Armor), "Armor cannot be negative.");
        }

        ValidateMultiplier(nameof(MoveSpeedMultiplier), MoveSpeedMultiplier);
        ValidateMultiplier(nameof(DamageMultiplier), DamageMultiplier);
        ValidateMultiplier(nameof(AttackSpeedMultiplier), AttackSpeedMultiplier);
        ValidateMultiplier(nameof(CooldownRecoveryMultiplier), CooldownRecoveryMultiplier);
        ValidateMultiplier(nameof(PickupRangeMultiplier), PickupRangeMultiplier);
        ValidateMultiplier(nameof(ProjectileDamageMultiplier), ProjectileDamageMultiplier);
        ValidateMultiplier(nameof(AreaDamageMultiplier), AreaDamageMultiplier);
        ValidateMultiplier(nameof(AreaRadiusMultiplier), AreaRadiusMultiplier);
        ValidateMultiplier(nameof(LifeFlaskEffectMultiplier), LifeFlaskEffectMultiplier);
        ValidateMultiplier(nameof(ItemQuantityMultiplier), ItemQuantityMultiplier);
        ValidateMultiplier(nameof(IncomingDamageMultiplier), IncomingDamageMultiplier);
        if (AilmentChanceBonus < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(AilmentChanceBonus), "Ailment chance bonus cannot be negative.");
        }

        ValidateMultiplier(nameof(AilmentDurationMultiplier), AilmentDurationMultiplier);
        ValidateMultiplier(nameof(DamageOverTimeMultiplier), DamageOverTimeMultiplier);
        ValidateMultiplier(nameof(PhysicalDamageMultiplier), PhysicalDamageMultiplier);
        ValidateMultiplier(nameof(FireDamageMultiplier), FireDamageMultiplier);
        ValidateMultiplier(nameof(ColdDamageMultiplier), ColdDamageMultiplier);
        ValidateMultiplier(nameof(LightningDamageMultiplier), LightningDamageMultiplier);
        ValidateMultiplier(nameof(PoisonDamageMultiplier), PoisonDamageMultiplier);
    }

    public static Stats Combine(Stats baseStats, Stats bonus)
    {
        ArgumentNullException.ThrowIfNull(baseStats);
        ArgumentNullException.ThrowIfNull(bonus);
        baseStats.Validate();
        bonus.Validate();

        return new Stats
        {
            MaxHp = baseStats.MaxHp + bonus.MaxHp,
            MoveSpeedMultiplier = baseStats.MoveSpeedMultiplier * bonus.MoveSpeedMultiplier,
            DamageMultiplier = baseStats.DamageMultiplier * bonus.DamageMultiplier,
            AttackSpeedMultiplier = baseStats.AttackSpeedMultiplier * bonus.AttackSpeedMultiplier,
            CooldownRecoveryMultiplier = baseStats.CooldownRecoveryMultiplier * bonus.CooldownRecoveryMultiplier,
            PickupRangeMultiplier = baseStats.PickupRangeMultiplier * bonus.PickupRangeMultiplier,
            ProjectileDamageMultiplier = baseStats.ProjectileDamageMultiplier * bonus.ProjectileDamageMultiplier,
            AreaDamageMultiplier = baseStats.AreaDamageMultiplier * bonus.AreaDamageMultiplier,
            AreaRadiusMultiplier = baseStats.AreaRadiusMultiplier * bonus.AreaRadiusMultiplier,
            Armor = baseStats.Armor + bonus.Armor,
            ProjectileCountBonus = baseStats.ProjectileCountBonus + bonus.ProjectileCountBonus,
            AilmentChanceBonus = baseStats.AilmentChanceBonus + bonus.AilmentChanceBonus,
            AilmentDurationMultiplier = baseStats.AilmentDurationMultiplier * bonus.AilmentDurationMultiplier,
            DamageOverTimeMultiplier = baseStats.DamageOverTimeMultiplier * bonus.DamageOverTimeMultiplier,
            LifeFlaskEffectMultiplier = baseStats.LifeFlaskEffectMultiplier * bonus.LifeFlaskEffectMultiplier,
            ItemQuantityMultiplier = baseStats.ItemQuantityMultiplier * bonus.ItemQuantityMultiplier,
            IncomingDamageMultiplier = baseStats.IncomingDamageMultiplier * bonus.IncomingDamageMultiplier,
            PhysicalDamageMultiplier = baseStats.PhysicalDamageMultiplier * bonus.PhysicalDamageMultiplier,
            FireDamageMultiplier = baseStats.FireDamageMultiplier * bonus.FireDamageMultiplier,
            ColdDamageMultiplier = baseStats.ColdDamageMultiplier * bonus.ColdDamageMultiplier,
            LightningDamageMultiplier = baseStats.LightningDamageMultiplier * bonus.LightningDamageMultiplier,
            PoisonDamageMultiplier = baseStats.PoisonDamageMultiplier * bonus.PoisonDamageMultiplier,
            FireResistance = baseStats.FireResistance + bonus.FireResistance,
            ColdResistance = baseStats.ColdResistance + bonus.ColdResistance,
            LightningResistance = baseStats.LightningResistance + bonus.LightningResistance,
            PoisonResistance = baseStats.PoisonResistance + bonus.PoisonResistance,
        };
    }

    public bool EquivalentTo(Stats other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return MaxHp == other.MaxHp
            && MoveSpeedMultiplier == other.MoveSpeedMultiplier
            && DamageMultiplier == other.DamageMultiplier
            && AttackSpeedMultiplier == other.AttackSpeedMultiplier
            && CooldownRecoveryMultiplier == other.CooldownRecoveryMultiplier
            && PickupRangeMultiplier == other.PickupRangeMultiplier
            && ProjectileDamageMultiplier == other.ProjectileDamageMultiplier
            && AreaDamageMultiplier == other.AreaDamageMultiplier
            && AreaRadiusMultiplier == other.AreaRadiusMultiplier
            && Armor == other.Armor
            && ProjectileCountBonus == other.ProjectileCountBonus
            && AilmentChanceBonus == other.AilmentChanceBonus
            && AilmentDurationMultiplier == other.AilmentDurationMultiplier
            && DamageOverTimeMultiplier == other.DamageOverTimeMultiplier
            && LifeFlaskEffectMultiplier == other.LifeFlaskEffectMultiplier
            && ItemQuantityMultiplier == other.ItemQuantityMultiplier
            && IncomingDamageMultiplier == other.IncomingDamageMultiplier
            && PhysicalDamageMultiplier == other.PhysicalDamageMultiplier
            && FireDamageMultiplier == other.FireDamageMultiplier
            && ColdDamageMultiplier == other.ColdDamageMultiplier
            && LightningDamageMultiplier == other.LightningDamageMultiplier
            && PoisonDamageMultiplier == other.PoisonDamageMultiplier
            && FireResistance == other.FireResistance
            && ColdResistance == other.ColdResistance
            && LightningResistance == other.LightningResistance
            && PoisonResistance == other.PoisonResistance;
    }

    private static void ValidateMultiplier(string name, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Multiplier must be finite and non-negative.");
        }
    }
}
