using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class StatsTests
{
    [Fact]
    public void New_stats_are_neutral_for_multipliers_and_zero_for_additive_values()
    {
        var stats = new Stats();

        Assert.Equal(0, stats.MaxHp);
        Assert.Equal(0, stats.Armor);
        Assert.Equal(0, stats.ProjectileCountBonus);
        Assert.Equal(1.0, stats.DamageMultiplier);
        Assert.Equal(1.0, stats.ProjectileDamageMultiplier);
        Assert.Equal(1.0, stats.AreaDamageMultiplier);
        Assert.Equal(0, stats.FireResistance);
        Assert.Equal(0, stats.PoisonResistance);
        stats.Validate();
    }

    [Fact]
    public void Combine_adds_integer_values_and_multiplies_multipliers()
    {
        var baseStats = new Stats
        {
            MaxHp = 100,
            Armor = 12,
            ProjectileCountBonus = 1,
            DamageMultiplier = 1.25,
            ProjectileDamageMultiplier = 1.10,
            FireResistance = 20,
        };
        var bonus = new Stats
        {
            MaxHp = 25,
            Armor = 8,
            ProjectileCountBonus = 2,
            DamageMultiplier = 1.20,
            ProjectileDamageMultiplier = 1.15,
            FireResistance = 15,
        };

        var result = Stats.Combine(baseStats, bonus);

        Assert.Equal(125, result.MaxHp);
        Assert.Equal(20, result.Armor);
        Assert.Equal(3, result.ProjectileCountBonus);
        Assert.Equal(1.50, result.DamageMultiplier, 10);
        Assert.Equal(1.265, result.ProjectileDamageMultiplier, 10);
        Assert.Equal(35, result.FireResistance);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Validate_rejects_invalid_multipliers(double invalidMultiplier)
    {
        var stats = new Stats { DamageMultiplier = invalidMultiplier };

        Assert.Throws<ArgumentOutOfRangeException>(() => stats.Validate());
    }

    [Fact]
    public void Validate_rejects_negative_health_and_armor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Stats { MaxHp = -1 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new Stats { Armor = -1 }.Validate());
    }

    [Fact]
    public void Combine_rejects_null_sources()
    {
        Assert.Throws<ArgumentNullException>(() => Stats.Combine(null!, new Stats()));
        Assert.Throws<ArgumentNullException>(() => Stats.Combine(new Stats(), null!));
    }
}
