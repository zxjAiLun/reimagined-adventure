using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class DamageContractTests
{
    [Fact]
    public void Damage_request_rejects_invalid_payloads()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DamageRequest(-1, DamageType.Physical, "test"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DamageRequest(1, (DamageType)999, "test"));
        Assert.Throws<ArgumentException>(
            () => new DamageRequest(1, DamageType.Physical, " "));
    }

    [Fact]
    public void Damage_result_exposes_blocked_and_killed_states()
    {
        Assert.True(new DamageResult(0, false).WasBlocked);
        Assert.False(new DamageResult(3, true).WasBlocked);
        Assert.True(new DamageResult(3, true).Killed);
    }
}
