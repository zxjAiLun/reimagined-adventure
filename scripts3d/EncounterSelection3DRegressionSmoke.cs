using System;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Validates deterministic encounter selection and the independent encounter
/// random namespace without instantiating a map.
/// </summary>
public partial class EncounterSelection3DRegressionSmoke : Node
{
    public override void _Ready()
    {
        try
        {
            var catalog = GD.Load<EncounterCatalogResource3D>(
                "res://resources/RunEncounterCatalog3D.tres");
            var catalogError = string.Empty;
            if (catalog == null || !catalog.IsValid(out catalogError))
            {
                Fail($"encounter catalog invalid: {catalogError}");
                return;
            }

            var candidates = catalog.ToDomainCandidates();
            var loot = new RandomService(101);
            var crafting = new RandomService(202);
            var events = new RandomService(303);
            var lootState = loot.State;
            var craftingState = crafting.State;
            var eventState = events.State;
            var first = EncounterSelection.Select(
                0xAABBCCDDEEFF0011UL,
                4,
                catalog.CatalogVersion,
                candidates);
            for (var index = 0; index < 20; index++)
            {
                if (EncounterSelection.Select(
                        0xAABBCCDDEEFF0011UL,
                        4,
                        catalog.CatalogVersion,
                        candidates) != first)
                {
                    Fail("encounter selection was not deterministic");
                    return;
                }
            }

            var mapOneCandidates = candidates.Where(candidate => candidate.IsEligible(1)).ToArray();
            var mapTwoCandidates = candidates.Where(candidate => candidate.IsEligible(2)).ToArray();
            var mapFourCandidates = candidates.Where(candidate => candidate.IsEligible(4)).ToArray();
            if (mapOneCandidates.Length != 1
                || mapOneCandidates[0].EncounterTier != 1
                || mapTwoCandidates.Any(candidate => candidate.EncounterTier >= 3)
                || !mapFourCandidates.Select(candidate => candidate.EncounterTier).Contains(2)
                || !mapFourCandidates.Select(candidate => candidate.EncounterTier).Contains(3)
                || !candidates.Select(candidate => candidate.EncounterTier).Contains(1))
            {
                Fail("encounter catalog level/tier eligibility was invalid");
                return;
            }

            var modifierSeed = RandomService.DeriveSeed(
                0xAABBCCDDEEFF0011UL,
                ((ulong)(uint)catalog.CatalogVersion << 32) | 4UL);
            if (first.SelectionSeed == modifierSeed
                || loot.State != lootState
                || crafting.State != craftingState
                || events.State != eventState)
            {
                Fail("encounter selection shared a modifier or run RNG stream");
                return;
            }

            var emptyCatalog = new EncounterCatalogResource3D();
            if (emptyCatalog.IsValid(out _))
            {
                Fail("empty encounter catalog unexpectedly passed validation");
                return;
            }

            var duplicateCatalog = new EncounterCatalogResource3D
            {
                Entries = new Godot.Collections.Array<EncounterCatalogEntryResource3D>
                {
                    new EncounterCatalogEntryResource3D
                    {
                        Definition = catalog.ResolveDefinition("quiet_coast_skirmish"),
                        DisplayName = "Duplicate A",
                    },
                    new EncounterCatalogEntryResource3D
                    {
                        Definition = catalog.ResolveDefinition("quiet_coast_skirmish"),
                        DisplayName = "Duplicate B",
                    },
                },
            };
            if (duplicateCatalog.IsValid(out _))
            {
                Fail("duplicate encounter id unexpectedly passed validation");
                return;
            }

            GD.Print($"ENCOUNTER_SELECTION_3D_REGRESSION_PASS deterministic=true ranges=true rng_unchanged=true seed_isolated=true selected={first.EncounterId}");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    private void Fail(string reason)
    {
        GD.PushError($"ENCOUNTER_SELECTION_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}
