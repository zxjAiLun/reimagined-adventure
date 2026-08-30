using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class SkillSupportTests
{
    [Fact]
    public void ProjectileSupportChangesCountAndDamage()
    {
        var skill = new SkillDefinition
        {
            Id = "contract_projectile",
            Name = "Contract Projectile",
            Slot = SkillSlot.Primary,
            CastType = SkillCastType.Projectile,
            BaseDamage = 10,
            ProjectileCount = 1,
            DamageType = DamageType.Physical,
        };
        var volley = SupportLibrary.Find("volley")!;

        Assert.True(SupportLibrary.SupportsSkill(volley, skill));
        Assert.Equal(3, SkillSupportMath.ProjectileCount(skill, Stats.Neutral, [volley]));
        Assert.Equal(8, SkillSupportMath.Damage(skill, Stats.Neutral, [volley]));
    }

    [Fact]
    public void IncompatibleSupportIsRejected()
    {
        var meteor = SkillLibrary.Meteor();
        var volley = SupportLibrary.Find("volley")!;

        Assert.False(SupportLibrary.SupportsSkill(volley, meteor));
        Assert.Throws<ArgumentException>(() => SkillSupportMath.Damage(meteor, Stats.Neutral, [volley]));
    }

    [Fact]
    public void PrimaryCooldownUsesAttackSpeedButAreaCooldownDoesNot()
    {
        var fastStats = new Stats { AttackSpeedMultiplier = 2.0 };
        var skill = SkillLibrary.SpreadShot();

        Assert.Equal(skill.CooldownSeconds / 2.0, SkillSupportMath.Cooldown(skill, fastStats));
        Assert.Equal(SkillLibrary.Meteor().CooldownSeconds, SkillSupportMath.Cooldown(SkillLibrary.Meteor(), fastStats));
    }

    [Fact]
    public void InvalidRequiredDamageTypeIsRejected()
    {
        var support = new SupportDefinition
        {
            Id = "invalid_element",
            Name = "Invalid Element",
            Kind = SupportKind.ElementalFocus,
            RequiredDamageType = (DamageType)999,
        };

        Assert.Throws<ArgumentOutOfRangeException>(support.Validate);
    }

    [Fact]
    public void AilmentSupportsUseDeclared_chances_and_compatibility()
    {
        var frostbite = SupportLibrary.Find("frostbite")!;
        var combustion = SupportLibrary.Find("combustion")!;
        var overload = SupportLibrary.Find("overload")!;

        Assert.True(SupportLibrary.SupportsSkill(frostbite, SkillLibrary.SpreadShot()));
        Assert.False(SupportLibrary.SupportsSkill(frostbite, SkillLibrary.Meteor()));
        Assert.True(SupportLibrary.SupportsSkill(combustion, SkillLibrary.Meteor()));
        Assert.True(SupportLibrary.SupportsSkill(overload, SkillLibrary.Pulse()));
        Assert.Equal(AilmentKind.Chilled, SkillSupportMath.Ailment(
            SkillLibrary.SpreadShot(), Stats.Neutral, [frostbite])!.Kind);
        Assert.Equal(35, SkillSupportMath.Ailment(
            SkillLibrary.SpreadShot(), Stats.Neutral, [frostbite])!.ChancePercent);
        Assert.Equal(50, SkillSupportMath.Ailment(
            SkillLibrary.Pulse(), Stats.Neutral, [overload])!.ChancePercent);
    }

    [Fact]
    public void CooldownRecoveryAnd_ailmentChance_are_effective_stats()
    {
        var stats = new Stats
        {
            CooldownRecoveryMultiplier = 2.0,
            AilmentChanceBonus = 20,
        };
        var overload = SupportLibrary.Find("overload")!;

        Assert.Equal(SkillLibrary.Pulse().CooldownSeconds / 2.0 * 1.15,
            SkillSupportMath.Cooldown(SkillLibrary.Pulse(), stats, [overload]), 10);
        Assert.Equal(70, SkillSupportMath.Ailment(
            SkillLibrary.Pulse(), stats, [overload])!.ChancePercent);
    }
}
