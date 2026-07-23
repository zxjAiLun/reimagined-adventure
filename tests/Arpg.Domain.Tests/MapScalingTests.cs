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
}
