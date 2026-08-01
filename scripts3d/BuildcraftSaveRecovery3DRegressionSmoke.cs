using System;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Stage 13 save smoke. It proves that non-empty inventory/equipment, stash,
/// Forge Fragments and the MapComplete phase survive a validated load.
/// </summary>
public partial class BuildcraftSaveRecovery3DRegressionSmoke : Node
{
    private double _elapsed;
    private bool _complete;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        if (_complete)
        {
            return;
        }

        _elapsed += delta;
        if (_elapsed < 0.4)
        {
            return;
        }

        try
        {
            RunSmoke();
            _complete = true;
            GD.Print("BUILDCRAFT_SAVE_RECOVERY_3D_REGRESSION_PASS inventory=true equipment=true stash=true currency=true phase=true");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            _complete = true;
            GD.PushError($"BUILDCRAFT_SAVE_RECOVERY_3D_REGRESSION_FAIL {exception.Message}");
            GetTree().Quit(1);
        }
    }

    private void RunSmoke()
    {
        var arena = GetNode<TestArena3D>("RunShell3D/TestArena3D");
        var run = GetNode<RunSessionNode>("RunShell3D");
        var flow = arena.GetNode<GameFlowController3D>("GameFlow3D");
        var rewards = arena.GetNode<MapRewardNode3D>("MapRewards3D");
        var build = arena.GetNode<BuildIntermissionController3D>("BuildIntermission3D");
        var route = arena.GetNode<AtlasRouteChoiceController3D>("AtlasRouteChoice3D");
        var save = arena.GetNode<SaveBoundaryNode3D>("SaveBoundary3D");
        var player = arena.GetNode<PlayerController3D>("Player3D");

        if (!run.TryCompleteCurrentAtlasMap()
            || !flow.RestoreState(GameFlowState.MapComplete))
        {
            throw new InvalidOperationException("could not prepare save recovery MapComplete state");
        }

        rewards.BeginChoice();
        if (!rewards.TryChooseReward(0) || !build.IsBuildManagement)
        {
            throw new InvalidOperationException("reward did not enter build management");
        }

        var generator = run.Session.CreateLootGenerator();
        var weapon = generator.GenerateItemDrop(new ItemRollContext(
            1, LootSourceKind.Reward, EquipmentSlot.Weapon, ForcedRarity: Rarity.Magic));
        var armor = generator.GenerateItemDrop(new ItemRollContext(
            1, LootSourceKind.Reward, EquipmentSlot.Armor, ForcedRarity: Rarity.Magic));
        if (!player.TryAddItem(weapon)
            || !player.TryAddItem(armor)
            || !player.TryEquipItem(weapon.Id)
            || !build.Currency.TryAdd(2)
            || !build.TryMoveInventoryToStash(armor.Id))
        {
            throw new InvalidOperationException("could not prepare non-empty build save state");
        }

        var expectedInventoryIds = player.Items.Select(item => item.Id).OrderBy(id => id).ToArray();
        var expectedEquipmentId = player.EquippedWeapon?.Id;
        var expectedStashId = build.StashItems.Single().Id;
        if (!save.TrySaveCurrentRun(out var saveError))
        {
            throw new InvalidOperationException($"build management save failed: {saveError}");
        }

        if (!build.TryMoveStashToInventory(expectedStashId)
            || !build.TryCompleteBuildForTest())
        {
            throw new InvalidOperationException("could not mutate build state before recovery");
        }

        if (!save.TryLoadAndApplyLastRun(out var restored, out var loadError))
        {
            throw new InvalidOperationException($"build management recovery failed: {loadError}");
        }

        if (restored.MapCompletePhase != MapCompletePhase.BuildManagement
            || !build.IsBuildManagement
            || build.Currency.ForgeFragments != 2
            || build.StashItems.Count != 1
            || build.StashItems[0].Id != expectedStashId
            || !expectedInventoryIds.SequenceEqual(player.Items.Select(item => item.Id).OrderBy(id => id))
            || player.EquippedWeapon?.Id != expectedEquipmentId)
        {
            throw new InvalidOperationException("build management save did not restore exact state");
        }

        if (!build.TryCompleteBuildForTest()
            || !route.ChoiceActive
            || !route.TrySelect(0)
            || string.IsNullOrWhiteSpace(run.PendingAtlasMapId))
        {
            throw new InvalidOperationException("route phase could not be reached after recovery");
        }

        if (!save.TrySaveCurrentRun(out saveError))
        {
            throw new InvalidOperationException($"route phase save failed: {saveError}");
        }

        if (!build.RestoreState(Array.Empty<Item>(), 0, MapCompletePhase.RewardChoice)
            || build.IsRouteChoice
            || route.ChoiceActive)
        {
            throw new InvalidOperationException("could not mutate route phase before second recovery");
        }

        if (!save.TryLoadAndApplyLastRun(out restored, out loadError)
            || restored.MapCompletePhase != MapCompletePhase.RouteChoice
            || !build.IsRouteChoice
            || !route.ChoiceActive
            || run.PendingAtlasMapId != route.SelectedMapId)
        {
            throw new InvalidOperationException($"route phase recovery failed: {loadError}");
        }
    }
}
