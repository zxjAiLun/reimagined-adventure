using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class MapModifierTests
{
    [Fact]
    public void ModifierLibraryContainsBaselineAndRiskRewardEntries()
    {
        foreach (var definition in MapModifierLibrary.All)
        {
            definition.Validate();
        }

        Assert.Equal(1.0, MapModifierLibrary.Find("quiet-coast")!.Effects.MonsterHpMultiplier);
        Assert.True(MapModifierLibrary.Find("frenzied-march")!.Effects.MonsterDamageBonus > 0);
    }

    [Fact]
    public void ModifierEffectsIncreaseScalingWithoutChangingBaseContract()
    {
        var modifier = MapModifierLibrary.Find("hardened-front")!.Effects;
        var boss = new BossScalingProfile { HpMultiplier = 2.0 };

        Assert.True(MapScaling.EnemyHp(5, modifier) > MapScaling.EnemyHp(5, new MapModifierStats()));
        Assert.True(MapScaling.BossHp(5, modifier, boss) > MapScaling.BossHp(5, new MapModifierStats(), boss));
    }

    [Fact]
    public void UnknownModifierIsNotAccepted()
    {
        Assert.Null(MapModifierLibrary.Find("not-a-real-modifier"));
    }
}
