using System;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Regression coverage for the three run-loop edge cases that are easy to
/// miss when the happy path is already green.
/// </summary>
public partial class StabilizationRegressionSmoke : Node
{
    public override void _Ready()
    {
        var arena = GetNode<Node2D>("TestArena");
        var player = arena.GetNode<PlayerController>("Player");
        var flow = arena.GetNode<GameFlowController>("GameFlowController");
        var run = arena.GetNode<RunSessionNode>("RunSession");
        var save = arena.GetNode<SaveBoundaryNode>("SaveBoundary");
        var passiveTree = player.GetNodeOrNull<PassiveTreeNode>("PassiveTree");
        var atlas = arena.GetNodeOrNull<AtlasNode>("Atlas");

        var transitionSession = new RunSession(777, 1);
        var failedTransition = transitionSession.TryAdvanceMap(() => false);
        var transitionRollbackPass = !failedTransition && transitionSession.MapLevel == 1;

        var expectedReward = new Stats
        {
            DamageMultiplier = 1.35,
            MaxHp = 10,
        };
        player.SetRewardStats(expectedReward);
        var previousState = new MinimalRunState
        {
            State = SaveRunState.Playing,
            RunSeed = run.Session.RunSeed,
            ItemSequence = run.Session.ItemSequence,
            MapLevel = run.Session.MapLevel,
            LootRandomState = run.Session.LootRandom.State,
            CraftingRandomState = run.Session.CraftingRandom.State,
            EventRandomState = run.Session.EventRandom.State,
            PlayerMaxHealth = player.MaxHealth,
            PlayerCurrentHealth = player.CurrentHealth,
            RewardStats = expectedReward,
            InventoryItemIds = Array.Empty<string>(),
            InventoryItems = Array.Empty<Item>(),
            PassiveAllocatedIndices = passiveTree?.State.AllocatedIndices ?? Array.Empty<int>(),
            AtlasUnlockedMapIds = atlas?.State.UnlockedMapIds.ToArray() ?? Array.Empty<string>(),
            AtlasCompletedMapIds = atlas?.State.CompletedMapIds.ToArray() ?? Array.Empty<string>(),
        };
        player.SetRewardStats(Stats.Neutral);
        save.TryRollback(previousState, passiveTree, atlas);
        var rewardRollbackPass = player.RewardStats.EquivalentTo(expectedReward);

        var saved = save.TrySaveCurrentRun(out var saveError);
        player.ApplyDamage(new DamageRequest(9999, DamageType.Physical, "regression_player_death"));
        var deadPhysicsStopped = !player.IsPhysicsProcessing();
        var loadError = string.Empty;
        var restored = saved && save.TryLoadAndApplyLastRun(out _, out loadError);
        var physicsRestorePass = restored
            && deadPhysicsStopped
            && player.IsAlive
            && player.IsPhysicsProcessing()
            && flow.State == GameFlowState.Playing
            && !GetTree().Paused;

        new MinimalSaveService().Delete();
        if (transitionRollbackPass && rewardRollbackPass && physicsRestorePass)
        {
            GD.Print("STABILIZATION_REGRESSION_PASS map_level=true reward_stats=true player_physics=true");
            GetTree().Quit(0);
            return;
        }

        GD.PrintErr($"STABILIZATION_REGRESSION_FAIL map_level={transitionRollbackPass} reward_stats={rewardRollbackPass} player_physics={physicsRestorePass} saveError={saveError} loadError={loadError}");
        GetTree().Quit(1);
    }
}
