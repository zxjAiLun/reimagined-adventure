using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class EncounterSelectionTests
{
    [Fact]
    public void SelectionIsDeterministicAndDoesNotConsumeRunStreams()
    {
        var candidates = new[]
        {
            new EncounterCandidate("skirmish", 1, 1, 0, 3),
            new EncounterCandidate("crossfire", 2, 2, 0, 2),
            new EncounterCandidate("siege", 3, 4, 0, 1),
        };
        var loot = new RandomService(11);
        var crafting = new RandomService(22);
        var events = new RandomService(33);
        var lootState = loot.State;
        var craftingState = crafting.State;
        var eventState = events.State;

        var first = EncounterSelection.Select(5573589318791800903UL, 4, 1, candidates);
        for (var index = 0; index < 20; index++)
        {
            Assert.Equal(
                first,
                EncounterSelection.Select(5573589318791800903UL, 4, 1, candidates));
        }

        var modifierSeed = RandomService.DeriveSeed(
            5573589318791800903UL,
            ((ulong)1 << 32) | 4UL);
        Assert.NotEqual(modifierSeed, first.SelectionSeed);
        Assert.Equal(lootState, loot.State);
        Assert.Equal(craftingState, crafting.State);
        Assert.Equal(eventState, events.State);
    }

    [Fact]
    public void SelectionFiltersByLevelAndTier()
    {
        var candidates = new[]
        {
            new EncounterCandidate("tier-one", 1, 1, 1, int.MaxValue),
            new EncounterCandidate("tier-two", 2, 2, 3, 1),
            new EncounterCandidate("tier-three", 3, 4, 0, 1),
        };

        var mapOne = EncounterSelection.Select(7, 1, 1, candidates);
        var mapTwo = EncounterSelection.Select(7, 2, 1, candidates);
        var mapFour = EncounterSelection.Select(7, 4, 1, candidates);
        Assert.Equal("tier-one", mapOne.EncounterId);
        Assert.Equal(1, mapOne.EncounterTier);
        Assert.Contains(mapTwo.EncounterId, new[] { "tier-two" });
        Assert.Contains(mapFour.EncounterId, new[] { "tier-three" });
    }

    [Fact]
    public void InvalidCatalogsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => EncounterSelection.Select(
            1,
            1,
            1,
            Array.Empty<EncounterCandidate>()));
        Assert.Throws<ArgumentException>(() => EncounterSelection.Select(
            1,
            1,
            1,
            new[]
            {
                new EncounterCandidate("duplicate", 1),
                new EncounterCandidate("duplicate", 1),
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() => EncounterSelection.Select(
            1,
            1,
            1,
            new[] { new EncounterCandidate("bad-tier", 0) }));
        Assert.Throws<ArgumentException>(() => EncounterSelection.Select(
            1,
            4,
            1,
            new[] { new EncounterCandidate("map-one", 1, 1, 1) }));
    }

    [Fact]
    public void CatalogVersionChangesTheSelectionSeed()
    {
        var candidates = new[] { new EncounterCandidate("skirmish", 1) };
        var versionOne = EncounterSelection.Select(99, 2, 1, candidates);
        var versionTwo = EncounterSelection.Select(99, 2, 2, candidates);
        Assert.NotEqual(versionOne.SelectionSeed, versionTwo.SelectionSeed);
        Assert.Equal(versionOne.EncounterId, versionTwo.EncounterId);
    }
}
