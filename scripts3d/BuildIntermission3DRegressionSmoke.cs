using System;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Stage 13 smoke for the MapComplete build boundary. It exercises the real
/// reward -> build management -> route order and keeps item/crafting rules in
/// the Domain transaction service.
/// </summary>
public partial class BuildIntermission3DRegressionSmoke : Node
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
            GD.Print("BUILD_INTERMISSION_3D_REGRESSION_PASS phase_order=true stash=true reforge=true route_gate=true");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            _complete = true;
            GD.PushError($"BUILD_INTERMISSION_3D_REGRESSION_FAIL {exception.Message}");
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
        var player = arena.GetNode<PlayerController3D>("Player3D");

        if (!run.TryCompleteCurrentAtlasMap()
            || !flow.RestoreState(GameFlowState.MapComplete))
        {
            throw new InvalidOperationException("could not prepare MapComplete build boundary");
        }

        rewards.BeginChoice();
        if (!rewards.TryChooseReward(0)
            || !build.IsBuildManagement
            || route.ChoiceActive)
        {
            throw new InvalidOperationException("reward did not enter build management before route choice");
        }

        var generator = run.Session.CreateLootGenerator();
        var weaponA = generator.GenerateItemDrop(new ItemRollContext(
            1, LootSourceKind.Reward, EquipmentSlot.Weapon, ForcedRarity: Rarity.Magic));
        var armor = generator.GenerateItemDrop(new ItemRollContext(
            1, LootSourceKind.Reward, EquipmentSlot.Armor, ForcedRarity: Rarity.Magic));
        var ring = generator.GenerateItemDrop(new ItemRollContext(
            1, LootSourceKind.Reward, EquipmentSlot.Ring, ForcedRarity: Rarity.Magic));
        var amulet = generator.GenerateItemDrop(new ItemRollContext(
            1, LootSourceKind.Reward, EquipmentSlot.Amulet, ForcedRarity: Rarity.Magic));
        var weaponB = generator.GenerateItemDrop(new ItemRollContext(
            1, LootSourceKind.Reward, EquipmentSlot.Weapon, ForcedRarity: Rarity.Magic));

        foreach (var item in new[] { weaponA, armor, ring, amulet, weaponB })
        {
            if (!player.TryAddItem(item))
            {
                throw new InvalidOperationException($"could not add {item.Slot} build item");
            }
        }

        foreach (var item in new[] { weaponA, armor, ring, amulet, weaponB })
        {
            if (!player.TryEquipItem(item.Id))
            {
                throw new InvalidOperationException($"could not equip {item.Slot} build item");
            }
        }

        if (player.Equipment.Items.Count != 4
            || player.Items.Count != 1
            || player.Items[0].Id != weaponA.Id)
        {
            throw new InvalidOperationException("build inventory/equipment swap was not deterministic");
        }

        if (!build.Currency.TryAdd(1)
            || !build.TryMoveInventoryToStash(weaponA.Id))
        {
            throw new InvalidOperationException("inventory to stash transaction failed");
        }

        if (!build.TryReforge(weaponA.Id, out var result, out var error)
            || result == null
            || !string.IsNullOrWhiteSpace(error)
            || result.CraftedItem.Id == weaponA.Id
            || build.Currency.ForgeFragments != 0
            || build.StashItems.Count != 1
            || build.StashItems[0].Id != result.CraftedItem.Id)
        {
            throw new InvalidOperationException($"stash reforge transaction failed: {error}");
        }

        if (!build.TryMoveStashToInventory(result.CraftedItem.Id)
            || player.Items.All(item => item.Id != result.CraftedItem.Id))
        {
            throw new InvalidOperationException("stash to inventory transaction failed");
        }

        if (!build.TryCompleteBuildForTest()
            || !build.IsRouteChoice
            || !route.ChoiceActive
            || route.OptionCount == 0
            || !route.TrySelect(0)
            || string.IsNullOrWhiteSpace(run.PendingAtlasMapId))
        {
            throw new InvalidOperationException("build completion did not unlock the route choice");
        }
    }
}
