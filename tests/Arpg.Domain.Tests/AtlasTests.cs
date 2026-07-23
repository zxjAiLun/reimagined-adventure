using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class AtlasTests
{
    [Fact]
    public void CompletingMapUnlocksNextMapInMinimumSlice()
    {
        var state = new AtlasState(AtlasLibrary.MinimumSlice());

        Assert.Contains(state.AvailableMaps, map => map.Id == "quiet-coast");
        Assert.False(state.IsUnlocked("hardened-frontier"));
        Assert.True(state.TryComplete("quiet-coast"));
        Assert.True(state.IsCompleted("quiet-coast"));
        Assert.True(state.IsUnlocked("hardened-frontier"));
        Assert.Contains(state.AvailableMaps, map => map.Id == "hardened-frontier");
    }

    [Fact]
    public void LockedMapCannotBeCompletedBeforePrerequisite()
    {
        var state = new AtlasState(AtlasLibrary.MinimumSlice());

        Assert.False(state.TryComplete("brimstone-caldera"));
        Assert.False(state.TryUnlock("brimstone-caldera"));
        Assert.True(state.TryComplete("quiet-coast"));
        Assert.True(state.TryComplete("hardened-frontier"));
        Assert.True(state.TryComplete("brimstone-caldera"));
    }

    [Fact]
    public void RestoreRejectsUnknownOrBrokenPrerequisiteState()
    {
        var state = new AtlasState(AtlasLibrary.MinimumSlice());

        Assert.False(state.TryRestore(
            ["quiet-coast", "missing"],
            ["quiet-coast"]));
        Assert.False(state.TryRestore(
            ["quiet-coast", "hardened-frontier"],
            ["hardened-frontier"]));
        Assert.True(state.TryRestore(
            ["quiet-coast", "hardened-frontier"],
            ["quiet-coast"]));
    }

    [Fact]
    public void CyclicAtlasDefinitionIsRejected()
    {
        var definition = new AtlasDefinition
        {
            Maps =
            [
                new AtlasMapDefinition { Id = "a", Name = "A", PrerequisiteMapId = "b" },
                new AtlasMapDefinition { Id = "b", Name = "B", PrerequisiteMapId = "a" },
            ],
        };

        Assert.Throws<ArgumentException>(() => definition.Validate());
    }
}
