using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class CombatFactionTests
{
    [Fact]
    public void PlayerDamageOnlyHitsEnemyByDefault()
    {
        var request = new DamageRequest(10, DamageType.Physical, "pulse", CombatFaction.Player);

        Assert.True(CombatTargeting.CanHit(request, new StubTarget(CombatFaction.Enemy)));
        Assert.False(CombatTargeting.CanHit(request, new StubTarget(CombatFaction.Player)));
    }

    [Fact]
    public void NeutralSourceAndExplicitFriendlyFireCanHitSameFaction()
    {
        var neutral = new DamageRequest(10, DamageType.Physical, "test");
        var friendlyFire = new DamageRequest(
            10,
            DamageType.Physical,
            "test-friendly-fire",
            CombatFaction.Player,
            canHitSameFaction: true);

        Assert.True(CombatTargeting.CanHit(neutral, new StubTarget(CombatFaction.Player)));
        Assert.True(CombatTargeting.CanHit(friendlyFire, new StubTarget(CombatFaction.Player)));
    }

    [Fact]
    public void InvalidSourceFactionIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DamageRequest(1, DamageType.Physical, "test", (CombatFaction)99));
    }

    private sealed class StubTarget(CombatFaction faction) : ICombatTarget
    {
        public CombatFaction Faction { get; } = faction;
        public bool IsAlive => true;
        public DamageResult ApplyDamage(DamageRequest request) => new(request.RawDamage, false);
    }
}
