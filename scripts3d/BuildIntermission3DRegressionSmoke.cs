using System;
using System.Collections.Generic;
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
    private int _stage;
    private bool _complete;
    private TestArena3D _firstArena;
    private string[] _expectedInventoryIds = Array.Empty<string>();
    private Dictionary<EquipmentSlot, string> _expectedEquipmentIds = new();
    private string _expectedStashId = string.Empty;
    private int _expectedForgeFragments;
    private string _expectedPrimarySupport = string.Empty;
    private int _expectedItemSequence;
    private ulong _expectedLootRandom;
    private ulong _expectedCraftingRandom;
    private ulong _expectedEventRandom;
    private string _oldWorldDropId = string.Empty;

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
        if (_elapsed > 12.0)
        {
            var timeoutRun = GetNodeOrNull<RunSessionNode>("RunShell3D");
            var timeoutArena = GetTree().GetNodesInGroup("arena_3d")
                .OfType<TestArena3D>()
                .FirstOrDefault(candidate => !ReferenceEquals(candidate, _firstArena));
            var children = timeoutRun == null
                ? string.Empty
                : string.Join(',', timeoutRun.GetChildren().OfType<Node>().Select(child => child.Name.ToString()));
            Fail($"build intermission smoke timed out at stage {_stage} level={timeoutRun?.CurrentMapLevel} arena={timeoutArena != null} first_same={ReferenceEquals(timeoutArena, _firstArena)} children={children}");
            return;
        }

        try
        {
            if (_stage == 0 && _elapsed >= 0.4)
            {
                BeginBuildAndRoute();
            }
            else if (_stage == 1)
            {
                VerifyNextMapContinuity();
            }
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    private void BeginBuildAndRoute()
    {
        var arena = GetNode<TestArena3D>("RunShell3D/TestArena3D");
        var run = GetNode<RunSessionNode>("RunShell3D");
        var flow = arena.GetNode<GameFlowController3D>("GameFlow3D");
        var rewards = arena.GetNode<MapRewardNode3D>("MapRewards3D");
        var build = arena.GetNode<BuildIntermissionController3D>("BuildIntermission3D");
        var route = arena.GetNode<AtlasRouteChoiceController3D>("AtlasRouteChoice3D");
        var player = arena.GetNode<PlayerController3D>("Player3D");
        _firstArena = arena;

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

        if (!player.Skills.TryAttachSupport(SkillSlot.Primary, "volley"))
        {
            throw new InvalidOperationException("Volley support could not be configured during build");
        }

        var oldWorldItem = generator.GenerateItemDrop(new ItemRollContext(
            1, LootSourceKind.Feral, EquipmentSlot.Ring, ForcedRarity: Rarity.Normal));
        var dropScene = GD.Load<PackedScene>("res://scenes3d/ItemDrop3D.tscn")
            ?? throw new InvalidOperationException("ItemDrop3D scene is missing for map-scope test");
        var oldWorldDrop = dropScene.Instantiate<ItemDrop3D>();
        arena.AddChild(oldWorldDrop);
        oldWorldDrop.GlobalPosition = new Vector3(3.0f, 0.0f, 0.0f);
        oldWorldDrop.Configure(oldWorldItem);
        _oldWorldDropId = oldWorldItem.Id;

        if (!build.Currency.TryAdd(3)
            || !build.TryMoveInventoryToStash(weaponA.Id))
        {
            throw new InvalidOperationException("inventory to stash transaction failed");
        }

        if (!build.TryReforge(weaponA.Id, out var result, out var error)
            || result == null
            || !string.IsNullOrWhiteSpace(error)
            || result.CraftedItem.Id == weaponA.Id
            || build.Currency.ForgeFragments != 2
            || build.StashItems.Count != 1
            || build.StashItems[0].Id != result.CraftedItem.Id)
        {
            throw new InvalidOperationException($"stash reforge transaction failed: {error}");
        }

        if (player.Skills.SupportIdBySkillSlot.GetValueOrDefault(SkillSlot.Primary) != "volley")
        {
            throw new InvalidOperationException("configured Volley support was not retained before route");
        }

        _expectedInventoryIds = player.Items.Select(item => item.Id).OrderBy(id => id).ToArray();
        _expectedEquipmentIds = player.EquippedItems.ToDictionary(pair => pair.Key, pair => pair.Value.Id);
        _expectedStashId = result.CraftedItem.Id;
        _expectedForgeFragments = build.Currency.ForgeFragments;
        _expectedPrimarySupport = player.Skills.SupportIdBySkillSlot[SkillSlot.Primary];
        _expectedItemSequence = run.Session.ItemSequence;
        _expectedLootRandom = run.Session.LootRandom.State;
        _expectedCraftingRandom = run.Session.CraftingRandom.State;
        _expectedEventRandom = run.Session.EventRandom.State;

        if (!build.TryCompleteBuildForTest()
            || !build.IsRouteChoice
            || !route.ChoiceActive
            || route.OptionCount == 0
            || !route.TrySelect(0)
            || string.IsNullOrWhiteSpace(run.PendingAtlasMapId))
        {
            throw new InvalidOperationException("build completion did not unlock the route choice");
        }

        if (!route.TryConfirm())
        {
            throw new InvalidOperationException("selected route could not load the next map");
        }

        _elapsed = 0.0;
        _stage = 1;
    }

    private void VerifyNextMapContinuity()
    {
        var run = GetNode<RunSessionNode>("RunShell3D");
        var arena = GetTree().GetNodesInGroup("arena_3d")
            .OfType<TestArena3D>()
            .FirstOrDefault(candidate => !ReferenceEquals(candidate, _firstArena));
        if (run.CurrentMapLevel != 2 || arena == null || ReferenceEquals(arena, _firstArena))
        {
            return;
        }

        var flow = arena.GetNode<GameFlowController3D>("GameFlow3D");
        var build = arena.GetNode<BuildIntermissionController3D>("BuildIntermission3D");
        var player = arena.GetNode<PlayerController3D>("Player3D");
        var equipmentIds = player.EquippedItems.ToDictionary(pair => pair.Key, pair => pair.Value.Id);
        var inventoryIds = player.Items.Select(item => item.Id).OrderBy(id => id).ToArray();
        var support = player.Skills.SupportIdBySkillSlot.GetValueOrDefault(SkillSlot.Primary);
        var oldDropStillExists = GetTree().GetNodesInGroup("item_drops_3d")
            .OfType<ItemDrop3D>()
            .Any(drop => drop.Item?.Id == _oldWorldDropId);

        if (flow.State != GameFlowState.Playing
            || GetTree().Paused
            || !_expectedInventoryIds.SequenceEqual(inventoryIds)
            || !_expectedEquipmentIds.OrderBy(pair => pair.Key)
                .SequenceEqual(equipmentIds.OrderBy(pair => pair.Key))
            || build.StashItems.Count != 1
            || build.StashItems[0].Id != _expectedStashId
            || build.Currency.ForgeFragments != _expectedForgeFragments
            || support != _expectedPrimarySupport
            || run.Session.ItemSequence != _expectedItemSequence
            || run.Session.LootRandom.State != _expectedLootRandom
            || run.Session.CraftingRandom.State != _expectedCraftingRandom
            || run.Session.EventRandom.State != _expectedEventRandom
            || oldDropStillExists)
        {
            throw new InvalidOperationException(
                $"Map 2 build continuity failed level={run.CurrentMapLevel} flow={flow.State} paused={GetTree().Paused} "
                + $"inventory={string.Join(',', inventoryIds)} stash={build.StashItems.Count} currency={build.Currency.ForgeFragments} "
                + $"support={support} old_drop={oldDropStillExists}");
        }

        _complete = true;
        GD.Print("BUILD_INTERMISSION_3D_REGRESSION_PASS phase_order=true four_slots=true stash=true reforge=true support=true route_gate=true map2=true continuity=true old_drops_cleared=true");
        GetTree().Quit();
    }

    private void Fail(string reason)
    {
        _complete = true;
        GD.PushError($"BUILD_INTERMISSION_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}
