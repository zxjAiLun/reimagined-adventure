using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class EliteSelectionTests
{
    [Fact]
    public void SelectionUsesStableSpawnIdentityAndIgnoresCandidateOrder()
    {
        var ordered = EliteModifierLibrary.All.ToArray();
        var reversed = ordered.Reverse().ToArray();
        var first = EliteSelection.Select(
            77,
            991,
            mapLevel: 4,
            waveIndex: 2,
            spawnOrdinal: 7,
            isBoss: false,
            mapModifierId: "quiet-coast",
            ordered);

        Assert.Equal(
            first,
            EliteSelection.Select(
                77,
                991,
                mapLevel: 4,
                waveIndex: 2,
                spawnOrdinal: 7,
                isBoss: false,
                mapModifierId: "quiet-coast",
                reversed));
        Assert.NotEqual(
            first.SelectionSeed,
            EliteSelection.Select(
                77,
                991,
                mapLevel: 4,
                waveIndex: 2,
                spawnOrdinal: 8,
                isBoss: false,
                mapModifierId: "quiet-coast",
                ordered).SelectionSeed);
    }

    [Fact]
    public void EligibilityAndBossRulesMatchTheFrozenContract()
    {
        Assert.Equal(0, EliteSelection.EligibilityRatePercent(1));
        Assert.Equal(20, EliteSelection.EligibilityRatePercent(2));
        Assert.Equal(25, EliteSelection.EligibilityRatePercent(3));
        Assert.Equal(35, EliteSelection.EligibilityRatePercent(4));

        var mapOne = EliteSelection.Select(1, 2, 1, 0, 1, false, "quiet-coast");
        var boss = EliteSelection.Select(1, 2, 4, 0, 1, true, "quiet-coast");
        Assert.Null(mapOne.EliteModifierId);
        Assert.Null(boss.EliteModifierId);
    }

    [Fact]
    public void ModifierWeightsBiasSelectionWithoutChangingTheSelectionSeed()
    {
        var candidates = new[]
        {
            EliteModifierLibrary.Find("bulwark")!,
            EliteModifierLibrary.Find("frenzied")!,
        };
        var quietBulwark = 0;
        var hardenedBulwark = 0;
        var quietSelections = 0;
        var hardenedSelections = 0;
        for (var seed = 1UL; seed <= 4000UL; seed++)
        {
            var quiet = EliteSelection.Select(seed, 17, 4, 0, 1, false, "quiet-coast", candidates);
            var hardened = EliteSelection.Select(seed, 17, 4, 0, 1, false, "hardened-front", candidates);
            Assert.Equal(quiet.SelectionSeed, hardened.SelectionSeed);
            if (quiet.EliteModifierId != null)
            {
                quietSelections++;
                if (quiet.EliteModifierId == "bulwark")
                {
                    quietBulwark++;
                }
            }

            if (hardened.EliteModifierId != null)
            {
                hardenedSelections++;
                if (hardened.EliteModifierId == "bulwark")
                {
                    hardenedBulwark++;
                }
            }
        }

        Assert.True(quietSelections > 0);
        Assert.True(hardenedSelections > 0);
        Assert.True(hardenedBulwark > quietBulwark);
    }

    [Fact]
    public void DefinitionsValidateTheFourRuntimeContracts()
    {
        var bulwark = EliteModifierLibrary.Find("bulwark")!;
        var frenzied = EliteModifierLibrary.Find("frenzied")!;
        var frostbound = EliteModifierLibrary.Find("frostbound")!;
        var volcanic = EliteModifierLibrary.Find("volcanic")!;

        Assert.Equal(1.5, bulwark.HealthMultiplier);
        Assert.Equal(8, bulwark.ArmorBonus);
        Assert.Equal(0.9, bulwark.MoveSpeedMultiplier);
        Assert.Equal(1.15, frenzied.DamageMultiplier);
        Assert.Equal(1.2, frenzied.MoveSpeedMultiplier);
        Assert.Equal(1.25, frenzied.ActionSpeedMultiplier);
        Assert.Equal(1.15, frostbound.HealthMultiplier);
        Assert.Equal(AilmentKind.Chilled, frostbound.OnHitAilment?.Kind);
        Assert.Equal(1.1, volcanic.DamageMultiplier);
        Assert.Equal(0.8, volcanic.DeathEffect?.TelegraphDurationSeconds);
        Assert.Equal(1.8, volcanic.DeathEffect?.ExplosionRadius);
    }

    [Fact]
    public void InvalidEliteDefinitionsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EliteModifierDefinition(
            "bad",
            "Bad",
            1,
            1,
            HealthMultiplier: 0).Validate());
        Assert.Throws<ArgumentException>(() => EliteSelection.Select(
            1,
            2,
            2,
            0,
            1,
            false,
            "quiet-coast",
            new[]
            {
                EliteModifierLibrary.Find("bulwark")!,
                EliteModifierLibrary.Find("bulwark")!,
            }));
    }
}
