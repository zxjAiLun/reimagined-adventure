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
}
