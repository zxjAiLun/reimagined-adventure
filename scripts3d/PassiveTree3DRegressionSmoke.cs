using System;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Focused Stage 15 smoke for the resource-backed 12-node tree, formal build
/// input, prerequisite rejection, and the no-free-heal MaxHP contract.
/// </summary>
public partial class PassiveTree3DRegressionSmoke : Node
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
        if (_elapsed > 10.0)
        {
            Fail("passive tree smoke timed out");
            return;
        }

        try
        {
            var run = GetNodeOrNull<RunSessionNode>("RunShell3D");
            var arena = GetNodeOrNull<TestArena3D>("RunShell3D/TestArena3D");
            var player = arena?.GetNodeOrNull<PlayerController3D>("Player3D");
            var flow = arena?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
            var rewards = arena?.GetNodeOrNull<MapRewardNode3D>("MapRewards3D");
            var build = arena?.GetNodeOrNull<BuildIntermissionController3D>("BuildIntermission3D");
            var screen = arena?.GetNodeOrNull<InventoryScreenController3D>("Player3D/InventoryScreen3D");
            if (run == null || player == null || flow == null || rewards == null || build == null || screen == null)
            {
                return;
            }

            RunSmoke(run, player, flow, rewards, build, screen);
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    private void RunSmoke(
        RunSessionNode run,
        PlayerController3D player,
        GameFlowController3D flow,
        MapRewardNode3D rewards,
        BuildIntermissionController3D build,
        InventoryScreenController3D screen)
    {
        if (run.PassiveTree.Nodes.Count != 12
            || run.PassiveTree.Nodes.Select(node => node.Branch).Distinct().Count() != 4
            || run.PassiveTree.Nodes.Any(node => string.IsNullOrWhiteSpace(node.Id)))
        {
            throw new InvalidOperationException("formal RunPassiveTree3D resource is not the stable 12-node tree");
        }

        player.ApplyDamage(new DamageRequest(
            5,
            DamageType.Physical,
            "passive_tree_smoke_hit",
            CombatFaction.Enemy));
        var healthBeforePassive = player.CurrentHealth;
        var maxHealthBeforePassive = player.MaxHealth;

        if (!run.TryAwardExperience(130, "passive-tree-smoke")
            || run.CharacterLevel != 3
            || run.UnspentPassivePoints != 2)
        {
            throw new InvalidOperationException("passive tree smoke could not prepare level 3 progression");
        }

        if (!flow.RestoreState(GameFlowState.MapComplete))
        {
            throw new InvalidOperationException("could not enter build flow for passive smoke");
        }

        rewards.BeginChoice();
        var playerBuild = player.GetNode<PlayerBuildController3D>("PlayerBuildController3D");
        if (!rewards.TryChooseReward(0) || !build.IsBuildManagement || !playerBuild.IsOpen)
        {
            throw new InvalidOperationException("passive smoke did not enter formal BuildManagement");
        }

        if (run.TryAllocatePassive("rapid-fire")
            || run.UnspentPassivePoints != 2
            || run.PassiveTree.IsAllocated("rapid-fire"))
        {
            throw new InvalidOperationException("passive prerequisite failure was not atomic");
        }

        SendKey(Key.V);
        SendKey(Key.G);
        if (!run.PassiveTree.IsAllocated("sharpened-bolt")
            || run.UnspentPassivePoints != 1
            || playerBuild.PanelMode != BuildPanelMode.Passives)
        {
            throw new InvalidOperationException("formal passive keyboard allocation failed");
        }

        if (!run.TryAllocatePassive("vigour")
            || run.UnspentPassivePoints != 0
            || player.MaxHealth <= maxHealthBeforePassive
            || player.CurrentHealth != healthBeforePassive
            || !screen.PassiveText.Contains("Vigour", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("passive MaxHP allocation changed health incorrectly");
        }

        _complete = true;
        GD.Print($"PASSIVE_TREE_3D_REGRESSION_PASS nodes={run.PassiveTree.Nodes.Count} level={run.CharacterLevel} hp={player.CurrentHealth}/{player.MaxHealth}");
        GetTree().Quit(0);
    }

    private void SendKey(Key key)
    {
        GetViewport().PushInput(new InputEventKey
        {
            Keycode = key,
            PhysicalKeycode = key,
            Pressed = true,
        });
    }

    private void Fail(string message)
    {
        if (_complete)
        {
            return;
        }

        _complete = true;
        GD.PushError($"PASSIVE_TREE_3D_REGRESSION_FAIL {message}");
        GetTree().Quit(1);
    }
}
