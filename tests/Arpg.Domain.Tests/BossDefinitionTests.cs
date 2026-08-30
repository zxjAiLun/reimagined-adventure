using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class BossDefinitionTests
{
    [Fact]
    public void BrimstoneDefinitionContainsBothReadableAttacks()
    {
        var boss = BossLibrary.BrimstoneColossus();

        boss.Validate();
        Assert.Equal(160, boss.MaxHealth);
        Assert.Equal("Magma Slam", boss.Attack(BossAttackKind.MagmaSlam).Name);
        Assert.Equal("Flame Spear", boss.Attack(BossAttackKind.FlameSpear).Name);
        Assert.Equal(3, boss.Phases.Count);
        Assert.Equal(
            new[] { BossAttackKind.MagmaSlam, BossAttackKind.FlameSpear },
            boss.Phases[0].AttackPattern);
        Assert.Equal("phase-3", boss.Phases[^1].Id);
        Assert.Equal(0.70, boss.Phases[^1].RecoveryMultiplier);
    }

    [Fact]
    public void DuplicateBossAttackKindsAreRejected()
    {
        var attack = new BossAttackDefinition
        {
            Id = "slam",
            Name = "Slam",
            Kind = BossAttackKind.MagmaSlam,
            Damage = 1,
        };
        var boss = new BossDefinition
        {
            Id = "invalid",
            Name = "Invalid",
            MaxHealth = 1,
            Attacks = [attack, attack],
        };

        Assert.Throws<ArgumentException>(() => boss.Validate());
    }

    [Fact]
    public void InvalidBossPhaseThresholdsAndAttackReferencesAreRejected()
    {
        var attack = new BossAttackDefinition
        {
            Id = "slam",
            Name = "Slam",
            Kind = BossAttackKind.MagmaSlam,
            Damage = 1,
        };
        var invalidThresholds = new BossDefinition
        {
            Id = "invalid-phases",
            Name = "Invalid Phases",
            MaxHealth = 1,
            Attacks = [attack],
            Phases =
            [
                new BossPhaseDefinition("phase-a", 100, [BossAttackKind.MagmaSlam], 1.0, null, null),
                new BossPhaseDefinition("phase-b", 100, [BossAttackKind.MagmaSlam], 1.0, null, null),
            ],
        };
        var invalidAttack = new BossDefinition
        {
            Id = "invalid-attack",
            Name = "Invalid Attack",
            MaxHealth = 1,
            Attacks = [attack],
            Phases =
            [
                new BossPhaseDefinition("phase-a", 100, [BossAttackKind.FlameSpear], 1.0, null, null),
                new BossPhaseDefinition("phase-b", 35, [BossAttackKind.MagmaSlam], 1.0, null, null),
            ],
        };

        Assert.Throws<ArgumentException>(() => invalidThresholds.Validate());
        Assert.Throws<ArgumentException>(() => invalidAttack.Validate());
    }
}
