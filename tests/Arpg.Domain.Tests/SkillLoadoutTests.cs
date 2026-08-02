using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class SkillLoadoutTests
{
    [Fact]
    public void AttachAndDetachEnforceCompatibilityAndUniqueOwnership()
    {
        var loadout = new SkillLoadout(SkillLibrary.DefaultBar());

        Assert.True(loadout.TryAttach(SkillSlot.Primary, "volley"));
        Assert.False(loadout.TryAttach(SkillSlot.Secondary, "volley"));
        Assert.True(loadout.TryAttach(SkillSlot.Secondary, "amplify"));
        Assert.True(loadout.TryDetach(SkillSlot.Primary));
        Assert.Null(loadout.SupportFor(SkillSlot.Primary));
    }

    [Fact]
    public void LockedOrUnknownSupportCannotBeAttached()
    {
        var loadout = new SkillLoadout(SkillLibrary.DefaultBar(), ["volley"]);

        Assert.False(loadout.TryAttach(SkillSlot.Primary, "amplify"));
        Assert.False(loadout.TryAttach(SkillSlot.Primary, "missing"));
        Assert.True(loadout.TryAttach(SkillSlot.Primary, "volley"));
    }

    [Fact]
    public void RestoreIsAtomicWhenSavedConfigurationIsInvalid()
    {
        var loadout = new SkillLoadout(SkillLibrary.DefaultBar());
        Assert.True(loadout.TryAttach(SkillSlot.Primary, "volley"));

        Assert.Throws<ArgumentException>(() => loadout.Restore(
            ["volley", "amplify"],
            new Dictionary<SkillSlot, string>
            {
                [SkillSlot.Primary] = "volley",
                [SkillSlot.Secondary] = "volley",
            }));

        Assert.Equal("volley", loadout.SupportIdBySkillSlot[SkillSlot.Primary]);
        Assert.False(loadout.SupportIdBySkillSlot.ContainsKey(SkillSlot.Secondary));
    }

    [Fact]
    public void SupportMathUsesOneCurrentSupport()
    {
        var loadout = new SkillLoadout(SkillLibrary.DefaultBar());
        Assert.True(loadout.TryAttach(SkillSlot.Primary, "volley"));

        var skill = SkillLibrary.SpreadShot();
        var baseCount = SkillSupportMath.ProjectileCount(skill, Stats.Neutral);
        var supportedCount = SkillSupportMath.ProjectileCount(skill, Stats.Neutral, loadout.SupportsFor(SkillSlot.Primary));

        Assert.Equal(3, baseCount);
        Assert.Equal(5, supportedCount);
    }
}
