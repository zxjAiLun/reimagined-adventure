using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class MapScalingTests
{
    [Fact]
    public void EnemyAndBossScalingIsMonotonicWithMapLevel()
    {
        var modifier = new MapModifierStats();
        var boss = new BossScalingProfile { HpMultiplier = 3.0, DamageBonus = 2 };

        Assert.True(MapScaling.EnemyHp(2, modifier) >= MapScaling.EnemyHp(1, modifier));
        Assert.True(MapScaling.EnemyDamage(4, modifier) >= MapScaling.EnemyDamage(3, modifier));
        Assert.True(MapScaling.BossHp(4, modifier, boss) >= MapScaling.BossHp(3, modifier, boss));
        Assert.True(MapScaling.BossContactDamage(4, modifier, boss) >= MapScaling.BossContactDamage(3, modifier, boss));
    }

    [Fact]
    public void ModifierAndItemLevelRulesMatchFirstSliceContract()
    {
        var modifier = new MapModifierStats
        {
            MonsterHpMultiplier = 2.0,
            MonsterDamageBonus = 3,
            ItemLevelBonus = 2,
        };

        Assert.Equal(2, MapScaling.EnemyHp(1, modifier));
        Assert.Equal(4, MapScaling.EnemyDamage(1, modifier));
        Assert.Equal(3, MapScaling.ItemLevel(1, modifier));
    }

    [Fact]
    public void NonPositiveMapLevelIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MapScaling.EnemyHp(0, new MapModifierStats()));
    }

    [Fact]
    public void EntityBaseValuesAreScaledWithoutUsingGodotTypes()
    {
        var modifier = new MapModifierStats
        {
            MonsterHpMultiplier = 1.15,
            MonsterDamageBonus = 2,
            BossHpMultiplier = 1.05,
            BossDamageMultiplier = 1.10,
        };

        Assert.Equal(69, MapScaling.EnemyHp(40, 3, modifier));
        Assert.Equal(10, MapScaling.EnemyDamage(8, 3, modifier));
        Assert.Equal(290, MapScaling.BossHp(160, 3, modifier, new BossScalingProfile()));
        Assert.Equal(14, MapScaling.BossContactDamage(10, 3, modifier, new BossScalingProfile()));
    }

    [Fact]
    public void NeutralEntityValuesMatchRunProgressionContract()
    {
        var modifier = new MapModifierStats();
        var boss = new BossScalingProfile { HpMultiplier = 1.0, DamageBonus = 0 };

        Assert.Equal(new[] { 40, 50, 70 }, new[]
        {
            MapScaling.EnemyHp(40, 1, modifier),
            MapScaling.EnemyHp(40, 2, modifier),
            MapScaling.EnemyHp(40, 4, modifier),
        });
        Assert.Equal(new[] { 24, 30, 42 }, new[]
        {
            MapScaling.EnemyHp(24, 1, modifier),
            MapScaling.EnemyHp(24, 2, modifier),
            MapScaling.EnemyHp(24, 4, modifier),
        });
        Assert.Equal(new[] { 160, 200, 280 }, new[]
        {
            MapScaling.BossHp(160, 1, modifier, boss),
            MapScaling.BossHp(160, 2, modifier, boss),
            MapScaling.BossHp(160, 4, modifier, boss),
        });
    }

    [Fact]
    public void DamageDoesNotDecreaseAndMapFourAddsOneTier()
    {
        var modifier = new MapModifierStats();
        var boss = new BossScalingProfile();

        Assert.Equal(8, MapScaling.EnemyDamage(8, 1, modifier));
        Assert.Equal(8, MapScaling.EnemyDamage(8, 2, modifier));
        Assert.Equal(9, MapScaling.EnemyDamage(8, 4, modifier));
        Assert.Equal(4, MapScaling.EnemyDamage(4, 1, modifier));
        Assert.Equal(5, MapScaling.EnemyDamage(4, 4, modifier));
        Assert.Equal(14, MapScaling.BossContactDamage(14, 1, modifier, boss));
        Assert.Equal(15, MapScaling.BossContactDamage(14, 4, modifier, boss));
    }

    [Fact]
    public void ItemLevelFollowsMapLevel()
    {
        var modifier = new MapModifierStats();

        Assert.Equal(1, MapScaling.ItemLevel(1, modifier));
        Assert.Equal(2, MapScaling.ItemLevel(2, modifier));
        Assert.Equal(4, MapScaling.ItemLevel(4, modifier));
    }

    [Fact]
    public void VeryHighMapLevelSaturatesWithoutOverflow()
    {
        var modifier = new MapModifierStats
        {
            MonsterDamageBonus = int.MaxValue,
            ItemLevelBonus = int.MaxValue,
        };

        Assert.Equal(int.MaxValue, MapScaling.EnemyDamage(int.MaxValue, int.MaxValue, modifier));
        Assert.Equal(int.MaxValue, MapScaling.ItemLevel(int.MaxValue, modifier));
        Assert.Equal(
            int.MaxValue,
            MapScaling.BossContactDamage(int.MaxValue, int.MaxValue, modifier, new BossScalingProfile()));
    }
}
