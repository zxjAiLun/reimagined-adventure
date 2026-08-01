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

    [Fact]
    public void ModifierSelectionIsStableAndDoesNotConsumeRunStreams()
    {
        var candidates = new[]
        {
            new MapModifierCandidate("quiet-coast", 1, 0, 1),
            new MapModifierCandidate("hardened-front", 2, 0, 3),
        };
        var loot = new RandomService(1001);
        var crafting = new RandomService(1002);
        var events = new RandomService(1003);
        var lootState = loot.State;
        var craftingState = crafting.State;
        var eventState = events.State;

        var first = MapModifierSelection.Select(5573589318791800903UL, 2, 1, candidates);
        for (var index = 0; index < 20; index++)
        {
            var repeat = MapModifierSelection.Select(5573589318791800903UL, 2, 1, candidates);
            Assert.Equal(first, repeat);
        }

        Assert.Equal(lootState, loot.State);
        Assert.Equal(craftingState, crafting.State);
        Assert.Equal(eventState, events.State);
        Assert.Contains(first.ModifierId, new[] { "quiet-coast", "hardened-front" });
    }

    [Fact]
    public void ModifierSelectionFiltersByMapLevelBeforeWeighting()
    {
        var candidates = new[]
        {
            new MapModifierCandidate("level-one-only", 1, 1, int.MaxValue),
            new MapModifierCandidate("level-two-only", 2, 2, 1),
        };

        Assert.Equal(
            "level-one-only",
            MapModifierSelection.Select(7, 1, 1, candidates).ModifierId);
        Assert.Equal(
            "level-two-only",
            MapModifierSelection.Select(7, 2, 1, candidates).ModifierId);
    }

    [Fact]
    public void ModifierSelectionRejectsInvalidCatalogs()
    {
        Assert.Throws<ArgumentException>(() => MapModifierSelection.Select(
            1,
            1,
            1,
            Array.Empty<MapModifierCandidate>()));
        Assert.Throws<ArgumentException>(() => MapModifierSelection.Select(
            1,
            1,
            1,
            new[]
            {
                new MapModifierCandidate("duplicate", 1, 0, 1),
                new MapModifierCandidate("duplicate", 1, 0, 1),
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapModifierSelection.Select(
            1,
            1,
            1,
            new[] { new MapModifierCandidate("zero-weight", 1, 0, 0) }));
        Assert.Throws<ArgumentException>(() => MapModifierSelection.Select(
            1,
            4,
            1,
            new[] { new MapModifierCandidate("level-one-only", 1, 1, 1) }));
    }
}
