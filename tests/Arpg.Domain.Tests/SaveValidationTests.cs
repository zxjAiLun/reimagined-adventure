using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class SaveValidationTests
{
    [Fact]
    public void DefaultSnapshotIsValid()
    {
        var snapshot = new SaveSnapshot();

        Assert.True(snapshot.TryValidate(out var error), error);
    }

    [Theory]
    [InlineData(0x1234U, 1)]
    [InlineData(SaveSnapshot.ExpectedMagic, 99)]
    public void InvalidHeaderIsRejected(uint magic, int version)
    {
        var snapshot = new SaveSnapshot { Magic = magic, Version = version };

        Assert.False(snapshot.TryValidate(out _));
    }

    [Fact]
    public void InvalidResourceAndSelectionStateIsRejected()
    {
        var snapshot = new SaveSnapshot
        {
            PlayerMaxHealth = 100,
            PlayerCurrentHealth = 101,
            ManaCharges = 4,
            SelectedNextMapOption = -1,
            NextMapOptionChosen = true,
        };

        Assert.Throws<ArgumentException>(() => snapshot.Validate());
    }

    [Fact]
    public void MinimalRunStateRoundTripsThroughValidatedSnapshot()
    {
        var state = new MinimalRunState
        {
            State = SaveRunState.MapComplete,
            MapLevel = 3,
            PlayerMaxHealth = 120,
            PlayerCurrentHealth = 95,
            ManaCharges = 2,
            InventoryItemIds = ["drop_0001", "drop_0002"],
            EquippedWeaponId = "drop_0001",
        };

        var restored = SaveSnapshot.Capture(state).Restore();

        Assert.Equal(state.State, restored.State);
        Assert.Equal(state.MapLevel, restored.MapLevel);
        Assert.Equal(state.InventoryItemIds, restored.InventoryItemIds);
        Assert.Equal(state.EquippedWeaponId, restored.EquippedWeaponId);
    }

    [Fact]
    public void InventoryIdentityAndPlayingSelectionRulesAreRejected()
    {
        var duplicateItems = new SaveSnapshot
        {
            InventoryCount = 2,
            InventoryItemIds = ["same", "same"],
        };
        var playingWithReward = new SaveSnapshot
        {
            State = SaveRunState.Playing,
            SelectedMapRewardOption = 0,
            MapRewardChosen = true,
        };

        Assert.False(duplicateItems.TryValidate(out _));
        Assert.False(playingWithReward.TryValidate(out _));
    }
}
