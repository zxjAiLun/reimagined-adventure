using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// Verifies that map modifier selection is a pure, deterministic derivation
/// from run identity and catalog data. It must not advance any run-owned RNG.
/// </summary>
public partial class MapModifierSelection3DRegressionSmoke : Node
{
    public override void _Ready()
    {
        try
        {
            var catalog = GD.Load<MapModifierCatalogResource3D>(
                "res://resources/RunMapModifierCatalog3D.tres");
            var catalogError = string.Empty;
            if (catalog == null || !catalog.IsValid(out catalogError))
            {
                Fail($"catalog invalid: {catalogError}");
                return;
            }

            var candidates = catalog.ToDomainCandidates();
            var loot = new RandomService(0x1001UL);
            var crafting = new RandomService(0x2002UL);
            var events = new RandomService(0x3003UL);
            var lootState = loot.State;
            var craftingState = crafting.State;
            var eventState = events.State;
            var first = MapModifierSelection.Select(
                0xAABBCCDDEEFF0011UL,
                3,
                catalog.CatalogVersion,
                candidates);

            for (var index = 0; index < 20; index++)
            {
                var repeat = MapModifierSelection.Select(
                    0xAABBCCDDEEFF0011UL,
                    3,
                    catalog.CatalogVersion,
                    candidates);
                if (repeat.ModifierId != first.ModifierId
                    || repeat.SelectionSeed != first.SelectionSeed
                    || repeat.CandidateIndex != first.CandidateIndex)
                {
                    Fail("selection was not deterministic across repeated calls");
                    return;
                }
            }

            if (loot.State != lootState || crafting.State != craftingState || events.State != eventState)
            {
                Fail("modifier selection changed a run-owned RNG state");
                return;
            }

            var mapOne = MapModifierSelection.Select(
                0xAABBCCDDEEFF0011UL,
                1,
                catalog.CatalogVersion,
                candidates);
            if (string.IsNullOrWhiteSpace(mapOne.ModifierId)
                || !candidates[mapOne.CandidateIndex].IsEligible(1))
            {
                Fail("selection returned a candidate outside the map level range");
                return;
            }

            var restored = MapModifierSelection.Select(
                0xAABBCCDDEEFF0011UL,
                3,
                catalog.CatalogVersion,
                candidates);
            if (restored.ModifierId != first.ModifierId)
            {
                Fail("save/restore re-selection changed the modifier");
                return;
            }

            var rejectedEmpty = false;
            try
            {
                MapModifierSelection.Select(
                    1UL,
                    1,
                    catalog.CatalogVersion,
                    Array.Empty<MapModifierCandidate>());
            }
            catch (ArgumentException)
            {
                rejectedEmpty = true;
            }

            if (!rejectedEmpty)
            {
                Fail("empty catalog unexpectedly accepted");
                return;
            }

            var emptyResourceCatalog = new MapModifierCatalogResource3D();
            if (emptyResourceCatalog.IsValid(out _))
            {
                Fail("empty Godot catalog unexpectedly passed validation");
                return;
            }

            var invalidEntryCatalog = new MapModifierCatalogResource3D
            {
                Entries = new Godot.Collections.Array<MapModifierCatalogEntryResource3D>
                {
                    new MapModifierCatalogEntryResource3D
                    {
                        Modifier = GD.Load<MapModifierResource>("res://resources/QuietCoast.tres"),
                        Weight = 0,
                    },
                },
            };
            if (invalidEntryCatalog.IsValid(out _))
            {
                Fail("invalid Godot catalog entry unexpectedly passed validation");
                return;
            }

            GD.Print($"MAP_MODIFIER_SELECTION_3D_REGRESSION_PASS deterministic=true rng_unchanged=true selected={first.ModifierId}");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    private void Fail(string reason)
    {
        GD.PushError($"MAP_MODIFIER_SELECTION_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}
