namespace Arpg.Domain;

/// <summary>
/// Pure effective-skill calculations. Godot currently uses the default skill
/// bar without supports, but these rules are ready for the later content/UI
/// migration and remain independently testable.
/// </summary>
public static class SkillSupportMath
{
    public static int Damage(
        SkillDefinition skill,
        Stats stats,
        IEnumerable<SupportDefinition>? supports = null,
        double shrineMultiplier = 1.0)
    {
        var validSupports = ValidateSupports(skill, supports);
        var supportMultiplier = validSupports.Aggregate(
            1.0,
            (current, support) => current
                * support.DamageMultiplier
                * (support.RequiredDamageType == skill.DamageType
                    ? support.ElementalDamageMultiplier
                    : 1.0));
        var category = skill.CastType switch
        {
            SkillCastType.Projectile => SkillDamageCategory.Projectile,
            SkillCastType.SelfCenteredArea or SkillCastType.MouseTargetedArea => SkillDamageCategory.Area,
            _ => SkillDamageCategory.Generic,
        };
        return CombatMath.SkillDamage(
            skill.BaseDamage,
            stats,
            skill.DamageType,
            category,
            supportMultiplier,
            shrineMultiplier);
    }

    public static int ProjectileCount(
        SkillDefinition skill,
        Stats stats,
        IEnumerable<SupportDefinition>? supports = null)
    {
        var validSupports = ValidateSupports(skill, supports);
        return Math.Max(
            1,
            skill.ProjectileCount
                + stats.ProjectileCountBonus
                + validSupports.Sum(support => support.ExtraProjectileCount));
    }

    public static double SpreadAngle(
        SkillDefinition skill,
        IEnumerable<SupportDefinition>? supports = null)
    {
        var validSupports = ValidateSupports(skill, supports);
        return Math.Max(
            0.0,
            skill.SpreadAngleDegrees
                + validSupports.Sum(support => support.ExtraSpreadAngleDegrees));
    }

    public static double Radius(
        SkillDefinition skill,
        Stats stats,
        IEnumerable<SupportDefinition>? supports = null)
    {
        var validSupports = ValidateSupports(skill, supports);
        if (!skill.IsArea)
        {
            return skill.Radius;
        }

        return skill.Radius
            * stats.AreaRadiusMultiplier
            * validSupports.Aggregate(1.0, (current, support) => current * support.RadiusMultiplier);
    }

    public static double Cooldown(
        SkillDefinition skill,
        Stats stats,
        IEnumerable<SupportDefinition>? supports = null)
    {
        var validSupports = ValidateSupports(skill, supports);
        var cooldown = skill.CooldownSeconds
            * validSupports.Aggregate(1.0, (current, support) => current * support.CooldownMultiplier);
        return skill.Slot == SkillSlot.Primary
            ? cooldown / Math.Max(0.0001, stats.AttackSpeedMultiplier)
            : cooldown;
    }

    public static double ManaCost(
        SkillDefinition skill,
        IEnumerable<SupportDefinition>? supports = null)
    {
        var validSupports = ValidateSupports(skill, supports);
        return Math.Max(
            0.0,
            skill.ManaCost
                * validSupports.Aggregate(1.0, (current, support) => current * support.ManaCostMultiplier));
    }

    private static IReadOnlyList<SupportDefinition> ValidateSupports(
        SkillDefinition skill,
        IEnumerable<SupportDefinition>? supports)
    {
        ArgumentNullException.ThrowIfNull(skill);
        skill.Validate();
        var validSupports = supports?.ToArray() ?? Array.Empty<SupportDefinition>();
        foreach (var support in validSupports)
        {
            ArgumentNullException.ThrowIfNull(support);
            if (!SupportLibrary.SupportsSkill(support, skill))
            {
                throw new ArgumentException(
                    $"Support '{support.Name}' is not compatible with skill '{skill.Name}'.",
                    nameof(supports));
            }
        }

        return validSupports;
    }
}
