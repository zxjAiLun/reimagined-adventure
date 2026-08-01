using System;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Verifies map-local ownership for world drops and build-flow access while
/// two map instances overlap in one SceneTree.
/// </summary>
public partial class MapScope3DRegressionSmoke : Node
{
    private double _elapsed;
    private bool _complete;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        CreateScopeMap("MapA");
        CreateScopeMap("MapB");
    }

    public override void _Process(double delta)
    {
        if (_complete)
        {
            return;
        }

        _elapsed += delta;
        if (_elapsed > 10.0)
        {
            Fail("map scope smoke timed out");
            return;
        }

        if (_elapsed < 0.65)
        {
            return;
        }

        try
        {
            RunSmoke();
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    private void RunSmoke()
    {
        var mapA = GetNode<TestArena3D>("MapA");
        var mapB = GetNode<TestArena3D>("MapB");
        var playerA = mapA.GetNode<PlayerController3D>("Player3D");
        var playerB = mapB.GetNode<PlayerController3D>("Player3D");
        var flowA = mapA.GetNode<GameFlowController3D>("GameFlow3D");
        var flowB = mapB.GetNode<GameFlowController3D>("GameFlow3D");
        var buildB = mapB.GetNode<PlayerBuildController3D>("Player3D/PlayerBuildController3D");

        playerA.GlobalPosition = Vector3.Zero;
        playerB.GlobalPosition = Vector3.Zero;
        if (!flowA.RestoreState(GameFlowState.GameOver)
            || !flowB.RestoreState(GameFlowState.Playing))
        {
            throw new InvalidOperationException("could not establish overlapping map flow states");
        }

        var generator = new LootGenerator(72031);
        var itemA = generator.GenerateItemDrop(new ItemRollContext(
            1, LootSourceKind.Feral, EquipmentSlot.Ring, ForcedRarity: Rarity.Normal));
        var itemB = generator.GenerateItemDrop(new ItemRollContext(
            1, LootSourceKind.Feral, EquipmentSlot.Ring, ForcedRarity: Rarity.Normal));
        var dropScene = GD.Load<PackedScene>("res://scenes3d/ItemDrop3D.tscn")
            ?? throw new InvalidOperationException("ItemDrop3D scene is missing");
        var dropA = dropScene.Instantiate<ItemDrop3D>();
        var dropB = dropScene.Instantiate<ItemDrop3D>();
        mapA.AddChild(dropA);
        mapB.AddChild(dropB);
        dropA.GlobalPosition = Vector3.Zero;
        dropB.GlobalPosition = Vector3.Zero;
        dropA.Configure(itemA);
        dropB.Configure(itemB);

        if (!dropA.IsOwnedBy(playerA)
            || dropA.IsOwnedBy(playerB)
            || !dropB.IsOwnedBy(playerB)
            || dropB.IsOwnedBy(playerA))
        {
            throw new InvalidOperationException("world drops did not bind to their local maps");
        }

        if (dropA.TryCollect(playerB))
        {
            throw new InvalidOperationException("new-map player collected an old-map drop");
        }

        if (!playerB.TryPickupNearest(1.0f)
            || playerB.Items.All(item => item.Id != itemB.Id)
            || playerB.Items.Any(item => item.Id == itemA.Id)
            || dropA.Item?.Id != itemA.Id)
        {
            throw new InvalidOperationException("current-map pickup did not ignore the old-map drop");
        }

        if (!buildB.Open())
        {
            throw new InvalidOperationException("local Playing flow was blocked by old-map GameOver");
        }

        if (!buildB.Close()
            || GetTree().Paused
            || flowA.State != GameFlowState.GameOver
            || flowB.State != GameFlowState.Playing)
        {
            throw new InvalidOperationException("local build-flow pause state was not restored");
        }

        _complete = true;
        GD.Print("MAP_SCOPE_3D_REGRESSION_PASS drop_binding=true old_drop_rejected=true current_drop_collected=true local_build_flow=true");
        GetTree().Quit();
    }

    private void Fail(string reason)
    {
        _complete = true;
        GD.PushError($"MAP_SCOPE_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }

    private void CreateScopeMap(string mapName)
    {
        var scene = GD.Load<PackedScene>("res://scenes3d/TestArena3D.tscn")
            ?? throw new InvalidOperationException("TestArena3D scene is missing");
        var map = scene.Instantiate<TestArena3D>();
        map.Name = mapName;
        var smallRegion = map.GetNodeOrNull<NavigationRegion3D>("SmallNavigationRegion3D");
        var largeRegion = map.GetNodeOrNull<NavigationRegion3D>("LargeNavigationRegion3D");
        if (smallRegion != null)
        {
            smallRegion.Enabled = false;
        }

        if (largeRegion != null)
        {
            largeRegion.Enabled = false;
        }

        AddChild(map);
    }
}
