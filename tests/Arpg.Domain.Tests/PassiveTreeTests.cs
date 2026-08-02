using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class PassiveTreeTests
{
    [Fact]
    public void PrerequisiteMustBeAllocatedBeforeChild()
    {
        var state = new PassiveTreeState(PassiveTreeLibrary.MinimumSlice());

        Assert.False(state.TryAllocate(1));
        Assert.True(state.TryAllocate(0));
        Assert.True(state.TryAllocate(1));
        Assert.Equal(1.06, state.CombinedStats().AttackSpeedMultiplier);
    }

    [Fact]
    public void AllocatedStatsAggregateAcrossBranches()
    {
        var state = new PassiveTreeState(PassiveTreeLibrary.MinimumSlice());

        Assert.True(state.TryAllocate(0));
        Assert.True(state.TryAllocate(2));
        var stats = state.CombinedStats();

        Assert.Equal(1.08, stats.ProjectileDamageMultiplier);
        Assert.Equal(5, stats.MaxHp);
    }

    [Fact]
    public void RestoreRejectsMissingPrerequisiteAndDuplicates()
    {
        var state = new PassiveTreeState(PassiveTreeLibrary.MinimumSlice());

        Assert.False(state.TryRestore([1]));
        Assert.False(state.TryRestore([0, 0]));
        Assert.True(state.TryRestore([0, 1, 2]));
        Assert.True(state.IsAllocated(1));
    }

    [Fact]
    public void MasteryTreeUsesStableIdsAndHasFourBranches()
    {
        var definition = PassiveTreeLibrary.MasterySlice();
        definition.Validate();
        var state = new PassiveTreeState(definition);
        var progression = new CharacterProgressionState(130);

        Assert.Equal(12, definition.Nodes.Count);
        Assert.Equal(4, definition.Nodes.Select(node => node.Branch).Distinct().Count());
        Assert.False(state.CanAllocate("rapid-fire", progression.UnspentPassivePoints));
        Assert.True(state.TryAllocate("sharpened-bolt", progression));
        Assert.True(state.TryAllocate("rapid-fire", progression));
        Assert.Contains("rapid-fire", state.AllocatedNodeIds);
        Assert.Equal(1.08, state.CombinedStats().AttackSpeedMultiplier);
        Assert.Equal(0, progression.UnspentPassivePoints);
    }

    [Fact]
    public void PassiveDefinitionRejectsUnknownSelfAndCyclicPrerequisites()
    {
        var unknown = new PassiveTreeDefinition
        {
            Nodes =
            [
                new PassiveNodeDefinition
                {
                    Id = "a",
                    Name = "A",
                    Description = "A",
                    PrerequisiteIds = ["missing"],
                },
            ],
        };
        var cyclic = new PassiveTreeDefinition
        {
            Nodes =
            [
                new PassiveNodeDefinition
                {
                    Id = "a",
                    Name = "A",
                    Description = "A",
                    PrerequisiteIds = ["b"],
                },
                new PassiveNodeDefinition
                {
                    Id = "b",
                    Name = "B",
                    Description = "B",
                    PrerequisiteIds = ["a"],
                },
            ],
        };

        Assert.Throws<ArgumentException>(() => unknown.Validate());
        Assert.Throws<ArgumentException>(() => cyclic.Validate());
    }

    [Fact]
    public void StableRestoreRequiresCompletePrerequisiteChainAndExactCost()
    {
        var state = new PassiveTreeState(PassiveTreeLibrary.MasterySlice());

        Assert.False(state.CanRestore(["rapid-fire"], 1));
        Assert.False(state.TryRestore(["sharpened-bolt", "rapid-fire"], 1));
        Assert.True(state.TryRestore(["sharpened-bolt", "rapid-fire"], 2));
        Assert.Equal(["rapid-fire", "sharpened-bolt"], state.AllocatedNodeIds.OrderBy(id => id));
    }

    [Fact]
    public void LegacyPassiveIndicesMapToStableIds()
    {
        Assert.True(PassiveTreeLibrary.TryMapLegacyIndices([0, 1, 2], out var nodeIds));
        Assert.Equal(["sharpened-bolt", "rapid-fire", "vigour"], nodeIds);
        Assert.False(PassiveTreeLibrary.TryMapLegacyIndices([0, 0], out _));
        Assert.False(PassiveTreeLibrary.TryMapLegacyIndices([99], out _));
    }

    [Fact]
    public void AllocationFailureDoesNotSpendPointsOrMutateTree()
    {
        var progression = new CharacterProgressionState(50);
        var state = new PassiveTreeState(PassiveTreeLibrary.MasterySlice());

        Assert.False(state.TryAllocate("rapid-fire", progression));
        Assert.Equal(1, progression.UnspentPassivePoints);
        Assert.Empty(state.AllocatedNodeIds);

        Assert.True(state.TryAllocate("sharpened-bolt", progression));
        Assert.Equal(0, progression.UnspentPassivePoints);
        Assert.False(state.TryAllocate("rapid-fire", progression));
        Assert.False(state.IsAllocated("rapid-fire"));
    }

    [Fact]
    public void LegacyAllocationCannotExceedExperienceEarnedPoints()
    {
        Assert.False(PassiveTreeLibrary.TryMigrateLegacyIndices([0, 1], 50, out _, out _));
        Assert.True(PassiveTreeLibrary.TryMigrateLegacyIndices([0], 50, out var nodeIds, out var cost));
        Assert.Equal(["sharpened-bolt"], nodeIds);
        Assert.Equal(1, cost);
    }
}
