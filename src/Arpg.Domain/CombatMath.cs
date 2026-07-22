namespace Arpg.Domain;

public enum SkillDamageCategory
{
    Generic,
    Projectile,
    Area,
}

/// <summary>
/// Stateless combat formulas shared by tests and the future Godot adapter.
/// </summary>
public static class CombatMath
{
    public static int MitigatedDamage(int rawDamage, int armor)
    {
        if (rawDamage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rawDamage), "Raw damage cannot be negative.");
        }

        if (armor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(armor), "Armor cannot be negative.");
        }

        // This preserves the C++ contract: a positive hit always deals at
        // least one point after flat armor mitigation.
        return Math.Max(1, rawDamage - armor);
    }

    public static int ResistanceForDamageType(
        DamageType type,
        int fireResistance,
        int coldResistance,
        int lightningResistance,
        int poisonResistance = 0,
        int physicalResistance = 0)
    {
        return type switch
        {
            DamageType.Physical => physicalResistance,
            DamageType.Fire => fireResistance,
            DamageType.Cold => coldResistance,
            DamageType.Lightning => lightningResistance,
            DamageType.Poison => poisonResistance,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown damage type."),
        };
    }

    public static int DamageAfterResistance(
        int rawDamage,
        DamageType type,
        int fireResistance,
        int coldResistance,
        int lightningResistance,
        int poisonResistance = 0,
        int physicalResistance = 0)
    {
        if (rawDamage <= 0)
        {
            return 0;
        }

        var resistance = ResistanceForDamageType(
            type,
            fireResistance,
            coldResistance,
            lightningResistance,
            poisonResistance,
            physicalResistance);
        resistance = Math.Clamp(resistance, -100, 100);
        if (resistance >= 100)
        {
            return 0;
        }

        var scaledDamage = (long)rawDamage * (100 - resistance);
        var result = (scaledDamage + 99) / 100;
        return result > int.MaxValue ? int.MaxValue : Math.Max(1, (int)result);
    }

    public static int IncomingDamage(int rawDamage, Stats stats, DamageType type = DamageType.Physical)
    {
        ArgumentNullException.ThrowIfNull(stats);
        stats.Validate();
        if (rawDamage <= 0)
        {
            return 0;
        }

        var scaledDamage = ScaleWithMinimumOne(rawDamage, stats.IncomingDamageMultiplier);
        return DamageAfterResistance(
            scaledDamage,
            type,
            stats.FireResistance,
            stats.ColdResistance,
            stats.LightningResistance,
            stats.PoisonResistance);
    }

    public static int LifeFlaskHealAmount(int baseAmount, Stats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        stats.Validate();
        if (baseAmount <= 0)
        {
            return 0;
        }

        var result = Math.Ceiling(baseAmount * stats.LifeFlaskEffectMultiplier);
        return result > int.MaxValue ? int.MaxValue : Math.Max(0, (int)result);
    }

    public static int SkillDamage(
        int baseDamage,
        Stats stats,
        DamageType type,
        SkillDamageCategory category = SkillDamageCategory.Generic,
        double additionalMultiplier = 1.0,
        double shrineMultiplier = 1.0)
    {
        ArgumentNullException.ThrowIfNull(stats);
        stats.Validate();
        ValidateDamageType(type);
        ValidateCategory(category);
        ValidateMultiplier(nameof(additionalMultiplier), additionalMultiplier);
        ValidateMultiplier(nameof(shrineMultiplier), shrineMultiplier);

        if (baseDamage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseDamage), "Base damage cannot be negative.");
        }

        if (baseDamage == 0)
        {
            return 0;
        }

        var damage = baseDamage * stats.DamageMultiplier;
        damage *= category switch
        {
            SkillDamageCategory.Projectile => stats.ProjectileDamageMultiplier,
            SkillDamageCategory.Area => stats.AreaDamageMultiplier,
            SkillDamageCategory.Generic => 1.0,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown damage category."),
        };
        damage *= DamageTypeMultiplier(stats, type);
        damage *= additionalMultiplier;
        damage *= shrineMultiplier;

        return ScaleWithMinimumOne(damage);
    }

    public static double DamageTypeMultiplier(Stats stats, DamageType type)
    {
        ArgumentNullException.ThrowIfNull(stats);
        stats.Validate();
        ValidateDamageType(type);

        return type switch
        {
            DamageType.Physical => stats.PhysicalDamageMultiplier,
            DamageType.Fire => stats.FireDamageMultiplier,
            DamageType.Cold => stats.ColdDamageMultiplier,
            DamageType.Lightning => stats.LightningDamageMultiplier,
            DamageType.Poison => stats.PoisonDamageMultiplier,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown damage type."),
        };
    }

    private static int ScaleWithMinimumOne(int baseDamage, double multiplier)
    {
        ValidateMultiplier(nameof(multiplier), multiplier);
        return ScaleWithMinimumOne(baseDamage * multiplier);
    }

    private static int ScaleWithMinimumOne(double damage)
    {
        if (double.IsNaN(damage) || double.IsInfinity(damage) || damage < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(damage), "Damage must be finite and non-negative.");
        }

        var result = Math.Ceiling(damage);
        if (result > int.MaxValue)
        {
            return int.MaxValue;
        }

        return Math.Max(1, (int)result);
    }

    private static void ValidateDamageType(DamageType type)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown damage type.");
        }
    }

    private static void ValidateCategory(SkillDamageCategory category)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown skill damage category.");
        }
    }

    private static void ValidateMultiplier(string name, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Multiplier must be finite and non-negative.");
        }
    }
}
