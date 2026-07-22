using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class SkillDefinitionTests
{
    [Fact]
    public void Default_bar_matches_the_four_cpp_baseline_skills()
    {
        var bar = SkillLibrary.DefaultBar();

        Assert.Equal("Spread Shot", bar[SkillSlot.Primary].Name);
        Assert.Equal(SkillCastType.Projectile, bar[SkillSlot.Primary].CastType);
        Assert.Equal(3, bar[SkillSlot.Primary].ProjectileCount);
        Assert.Equal(30.0, bar[SkillSlot.Primary].SpreadAngleDegrees);
        Assert.Equal(0.35, bar[SkillSlot.Primary].CooldownSeconds);

        Assert.Equal("Meteor", bar[SkillSlot.Secondary].Name);
        Assert.Equal(DamageType.Fire, bar[SkillSlot.Secondary].DamageType);
        Assert.Equal(110.0, bar[SkillSlot.Secondary].Radius);
        Assert.Equal(4, bar[SkillSlot.Secondary].BaseDamage);
        Assert.Equal(0.55, bar[SkillSlot.Secondary].CastDelaySeconds);

        Assert.Equal("Pulse", bar[SkillSlot.Utility].Name);
        Assert.Equal(DamageType.Lightning, bar[SkillSlot.Utility].DamageType);
        Assert.Equal(160.0, bar[SkillSlot.Utility].Radius);
        Assert.Equal(3, bar[SkillSlot.Utility].BaseDamage);

        Assert.Equal("Dash", bar[SkillSlot.Movement].Name);
        Assert.Equal(SkillCastType.Dash, bar[SkillSlot.Movement].CastType);
        Assert.Equal(120.0, bar[SkillSlot.Movement].DashDistance);
        Assert.Equal(0.8, bar[SkillSlot.Movement].CooldownSeconds);
    }

    [Fact]
    public void Skill_bar_requires_one_valid_definition_per_slot()
    {
        var primary = SkillLibrary.SpreadShot();
        var meteor = SkillLibrary.Meteor();
        var pulse = SkillLibrary.Pulse();
        var dash = SkillLibrary.Dash();

        var bar = new SkillBar(primary, meteor, pulse, dash);

        Assert.Equal(4, bar.Definitions.Count);
        Assert.Same(primary, bar[SkillSlot.Primary]);
        Assert.Same(dash, bar.Get(SkillSlot.Movement));
    }

    [Fact]
    public void Invalid_skill_shape_is_rejected()
    {
        var invalid = new SkillDefinition
        {
            Id = "bad",
            Name = "Bad Dash",
            Slot = SkillSlot.Primary,
            CastType = SkillCastType.Dash,
            DashDistance = 0.0,
        };

        Assert.Throws<ArgumentException>(() => invalid.Validate());
    }

    [Fact]
    public void Skill_bar_rejects_duplicate_slots()
    {
        var duplicatePrimary = new SkillDefinition
        {
            Id = "other_primary",
            Name = "Other Primary",
            Slot = SkillSlot.Primary,
            CastType = SkillCastType.Projectile,
            BaseDamage = 1,
        };

        Assert.Throws<ArgumentException>(() => new SkillBar(
            SkillLibrary.SpreadShot(),
            duplicatePrimary,
            SkillLibrary.Pulse(),
            SkillLibrary.Dash()));
    }
}
