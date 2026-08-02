using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class AilmentTests
{
    [Fact]
    public void Burning_ticks_three_times_then_expires()
    {
        var ailments = new AilmentCollection();

        Assert.True(ailments.Apply(new AilmentApplication(
            AilmentKind.Burning,
            "meteor",
            5,
            AilmentCollection.BurningDurationSeconds,
            AilmentCollection.BurningTickIntervalSeconds,
            CombatFaction.Player)));

        Assert.Empty(ailments.Tick(0.99));
        Assert.Equal(5, ailments.Tick(0.01).Single().RawDamage);
        Assert.Equal(5, ailments.Tick(1.0).Single().RawDamage);
        Assert.Equal(5, ailments.Tick(1.0).Single().RawDamage);
        Assert.False(ailments.Has(AilmentKind.Burning));
    }

    [Fact]
    public void Stronger_ailment_replaces_weaker_and_weaker_does_not_stack()
    {
        var ailments = new AilmentCollection();
        Assert.True(ailments.Apply(new AilmentApplication(
            AilmentKind.Shocked, "first", 15, 3.0)));

        Assert.False(ailments.Apply(new AilmentApplication(
            AilmentKind.Shocked, "weaker", 10, 1.0)));
        Assert.Equal(15, ailments.Get(AilmentKind.Shocked)!.Potency);
        Assert.Equal("first", ailments.Get(AilmentKind.Shocked)!.SourceId);

        Assert.True(ailments.Apply(new AilmentApplication(
            AilmentKind.Shocked, "stronger", 20, 3.0)));
        Assert.Equal(20, ailments.Get(AilmentKind.Shocked)!.Potency);
        Assert.Equal("stronger", ailments.Get(AilmentKind.Shocked)!.SourceId);
    }

    [Fact]
    public void Weaker_ailment_can_extend_duration_without_changing_potency()
    {
        var ailments = new AilmentCollection();
        ailments.Apply(new AilmentApplication(AilmentKind.Chilled, "first", 20, 1.0));
        ailments.Tick(0.25);

        Assert.True(ailments.Apply(new AilmentApplication(AilmentKind.Chilled, "weaker", 10, 2.5)));
        Assert.Equal(20, ailments.Get(AilmentKind.Chilled)!.Potency);
        Assert.Equal("first", ailments.Get(AilmentKind.Chilled)!.SourceId);
        Assert.InRange(ailments.Get(AilmentKind.Chilled)!.RemainingSeconds, 2.49, 2.5);
    }

    [Fact]
    public void Shocked_and_chilled_expose_fixed_runtime_multipliers()
    {
        var ailments = new AilmentCollection();
        Assert.Equal(1.0, ailments.IncomingDamageMultiplier);
        Assert.Equal(1.0, ailments.MoveSpeedMultiplier);
        Assert.Equal(1.0, ailments.ActionSpeedMultiplier);

        ailments.Apply(new AilmentApplication(AilmentKind.Shocked, "shock", 15, 3.0));
        ailments.Apply(new AilmentApplication(AilmentKind.Chilled, "cold", 20, 2.5));

        Assert.Equal(1.15, ailments.IncomingDamageMultiplier, 10);
        Assert.Equal(0.80, ailments.MoveSpeedMultiplier, 10);
        Assert.Equal(0.80, ailments.ActionSpeedMultiplier, 10);
    }

    [Fact]
    public void Invalid_ailment_data_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AilmentApplicationDefinition((AilmentKind)999, 20).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AilmentApplication(AilmentKind.Burning, "burn", 1, 3.0, 0.0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AilmentApplication(AilmentKind.Chilled, "cold", 0, 2.5).Validate());
    }
}
