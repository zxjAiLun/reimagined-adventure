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
}
