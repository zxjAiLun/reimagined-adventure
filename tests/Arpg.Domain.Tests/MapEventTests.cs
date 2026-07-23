using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class MapEventTests
{
    [Fact]
    public void LootCacheActivatesOnceWithTwoDrops()
    {
        var state = new MapEventState();
        var definition = MapEventLibrary.LootCache();

        Assert.True(state.TryActivate(definition, out var activation));
        Assert.Equal(MapEventType.LootCache, activation!.Type);
        Assert.Equal(2, activation.ItemDropCount);
        Assert.True(state.Triggered);
        Assert.True(state.Completed);
        Assert.False(state.TryActivate(definition, out _));
    }

    [Fact]
    public void ShrineCarriesDamageBuffContract()
    {
        var state = new MapEventState();

        Assert.True(state.TryActivate(MapEventLibrary.Shrine(), out var activation));
        Assert.Equal(1.35, activation!.DamageMultiplier);
        Assert.Equal(20.0, activation.BuffDurationSeconds);
    }

    [Fact]
    public void InvalidEventDefinitionIsRejected()
    {
        var invalid = new MapEventDefinition
        {
            Id = "bad-cache",
            Name = "Bad Cache",
            Type = MapEventType.LootCache,
            ItemDropCount = 0,
        };

        Assert.Throws<ArgumentException>(() => invalid.Validate());
    }
}
