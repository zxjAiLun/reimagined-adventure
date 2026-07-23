using Arpg.Domain;
using Godot;

/// <summary>
/// Verifies the scene-owned RunShell can carry a completed map into the next
/// map while preserving the run payload and incrementing map scaling.
/// </summary>
public partial class RunLoopTransitionSmoke : Node
{
    public override void _Ready()
    {
        GetTree().CreateTimer(0.20, processAlways: true).Timeout += PrepareTransition;
    }

    private void PrepareTransition()
    {
        var shell = GetNode<RunSessionNode>("RunShell");
        var player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
        var boss = GetTree().GetFirstNodeInGroup("bosses") as BrimstoneColossusController;
        var flow = GetTree().GetFirstNodeInGroup("game_flows") as GameFlowController;
        var rewards = GetTree().GetFirstNodeInGroup("map_rewards") as MapRewardNode;
        var item = shell.GenerateWeaponDrop(1);
        var prepared = player != null
            && player.Inventory.TryAddItem(item)
            && player.Inventory.TryEquipNewestWeapon();

        if (!prepared || boss == null || flow == null || rewards == null)
        {
            Fail("could not prepare transition state");
            return;
        }

        boss.ApplyDamage(new DamageRequest(9999, DamageType.Physical, "run_loop_transition"));
        var completed = flow.State == GameFlowState.MapComplete && rewards.ChoiceActive;
        var rewardChosen = rewards.TryChooseReward(0);
        var started = shell.LoadNextMap();
        if (!completed || !rewardChosen || !started)
        {
            Fail($"completed={completed} reward={rewardChosen} started={started}");
            return;
        }

        GetTree().CreateTimer(0.30, processAlways: true).Timeout += VerifyNextMap;
    }

    private void VerifyNextMap()
    {
        var shell = GetNode<RunSessionNode>("RunShell");
        var player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
        var flow = GetTree().GetFirstNodeInGroup("game_flows") as GameFlowController;
        var modifier = GetTree().GetFirstNodeInGroup("map_modifiers") as MapModifierNode;
        var atlas = GetTree().GetFirstNodeInGroup("atlas") as AtlasNode;
        var valid = shell.CurrentMapLevel == 2
            && flow?.State == GameFlowState.Playing
            && !GetTree().Paused
            && player?.Inventory.ItemCount == 0
            && player.RewardStats.DamageMultiplier > 1.0
            && modifier?.MapLevel == 2
            && atlas?.State.IsCompleted("quiet-coast") == true;

        new MinimalSaveService().Delete();
        if (valid)
        {
            GD.Print("RUN_LOOP_TRANSITION_PASS map=2 inventory=true reward=true atlas=true");
            GetTree().Quit(0);
            return;
        }

        Fail($"map={shell.CurrentMapLevel} state={flow?.State} paused={GetTree().Paused} inventory={player?.Inventory.ItemCount} reward={player?.RewardStats.DamageMultiplier} modifier={modifier?.MapLevel} atlas={atlas?.State.IsCompleted("quiet-coast")}");
    }

    private void Fail(string detail)
    {
        GD.PrintErr($"RUN_LOOP_TRANSITION_FAIL {detail}");
        new MinimalSaveService().Delete();
        GetTree().Quit(1);
    }
}
