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
}
