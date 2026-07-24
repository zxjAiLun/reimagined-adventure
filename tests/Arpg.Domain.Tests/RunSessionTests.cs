using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class RunSessionTests
{
    [Fact]
    public void FailedMapSaveRestoresPreviousMapLevel()
    {
        var session = new RunSession(1234, 1);

        var advanced = session.TryAdvanceMap(() => false);

        Assert.False(advanced);
        Assert.Equal(1, session.MapLevel);
    }

    [Fact]
    public void SuccessfulMapSaveCommitsNextMapLevel()
    {
        var session = new RunSession(1234, 1);

        var advanced = session.TryAdvanceMap(() => true);

        Assert.True(advanced);
        Assert.Equal(2, session.MapLevel);
    }
}
