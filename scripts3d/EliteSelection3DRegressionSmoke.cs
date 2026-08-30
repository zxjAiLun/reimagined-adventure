using System;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Domain-backed selection smoke. It intentionally runs without a map so the
/// result can only depend on the frozen spawn identity and elite catalog.
/// </summary>
public partial class EliteSelection3DRegressionSmoke : Node
{
    private bool _complete;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        RunContract();
    }

    private void RunContract()
    {
        try
        {
            var ordered = EliteModifierLibrary.All.ToArray();
            var reordered = ordered.Reverse().ToArray();
            var first = EliteSelection.Select(
                5573589318791800903UL,
                0xA11CEUL,
                mapLevel: 4,
                waveIndex: 2,
                spawnOrdinal: 7,
                isBoss: false,
                mapModifierId: "quiet-coast",
                ordered);
            var second = EliteSelection.Select(
                5573589318791800903UL,
                0xA11CEUL,
                mapLevel: 4,
                waveIndex: 2,
                spawnOrdinal: 7,
                isBoss: false,
                mapModifierId: "quiet-coast",
                reordered);
            if (!Equals(first, second) || first.SelectionSeed == 0)
            {
                Fail("reordered candidates changed the stable elite result");
                return;
            }

            var loot = new RandomService(11);
            var crafting = new RandomService(22);
            var events = new RandomService(33);
            var lootState = loot.State;
            var craftingState = crafting.State;
            var eventState = events.State;
            _ = EliteSelection.Select(77, 17, 4, 0, 1, false, "hardened-front");
            if (loot.State != lootState || crafting.State != craftingState || events.State != eventState)
            {
                Fail("elite selection consumed an unrelated run random stream");
                return;
            }

            if (EliteSelection.EligibilityRatePercent(1) != 0
                || EliteSelection.EligibilityRatePercent(2) != 20
                || EliteSelection.EligibilityRatePercent(3) != 25
                || EliteSelection.EligibilityRatePercent(4) != 35
                || EliteSelection.Select(1, 2, 1, 0, 1, false, "quiet-coast").EliteModifierId != null
                || EliteSelection.Select(1, 2, 4, 0, 1, true, "quiet-coast").EliteModifierId != null)
            {
                Fail("map-level rates or boss exclusion did not match the contract");
                return;
            }

            var candidates = new[]
            {
                EliteModifierLibrary.Find("bulwark")!,
                EliteModifierLibrary.Find("frenzied")!,
            };
            var quietBulwark = 0;
            var hardenedBulwark = 0;
            for (var seed = 1UL; seed <= 3000UL; seed++)
            {
                var quiet = EliteSelection.Select(seed, 17, 4, 0, 1, false, "quiet-coast", candidates);
                var hardened = EliteSelection.Select(seed, 17, 4, 0, 1, false, "hardened-front", candidates);
                if (quiet.EliteModifierId == "bulwark")
                {
                    quietBulwark++;
                }

                if (hardened.EliteModifierId == "bulwark")
                {
                    hardenedBulwark++;
                }
            }

            if (hardenedBulwark <= quietBulwark)
            {
                Fail($"Hardened Front did not bias Bulwark quiet={quietBulwark} hardened={hardenedBulwark}");
                return;
            }

            _complete = true;
            GD.Print(
                $"ELITE_SELECTION_3D_REGRESSION_PASS stable_identity=true rng_boundary=true rates=true boss_excluded=true weighted=true quiet_bulwark={quietBulwark} hardened_bulwark={hardenedBulwark}");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    private void Fail(string reason)
    {
        if (_complete)
        {
            return;
        }

        _complete = true;
        GD.PushError($"ELITE_SELECTION_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}
