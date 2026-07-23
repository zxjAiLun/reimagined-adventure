using System;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Long-lived stabilization contract: the existing M7–20 nodes must compose
/// into one run without identity collisions or terminal-state leaks.
/// </summary>
public partial class StabilizationRunLoopSmoke : Node
{
    public override void _Ready()
    {
        var arena = GetNode<Node2D>("TestArena");
        var player = arena.GetNode<PlayerController>("Player");
        var flow = arena.GetNode<GameFlowController>("GameFlowController");
        var run = arena.GetNode<RunSessionNode>("RunSession");
        var mapEvent = arena.GetNode<MapEventNode>("MapEvent");
        var interaction = arena.GetNode<InteractionController>("InteractionController");
        var rewards = arena.GetNode<MapRewardNode>("MapRewards");
        var boss = arena.GetNode<BrimstoneColossusController>("BrimstoneColossus");
        var save = arena.GetNode<SaveBoundaryNode>("SaveBoundary");

        var first = run.GenerateWeaponDrop(1);
        var second = run.CraftingGenerator.GenerateWeaponDropForBase(first.BaseId, 1, first.Id);
        var third = run.GenerateWeaponDrop(1, boss: true);
        var identityPass = new[] { first.Id, second.Id, third.Id }.Distinct(StringComparer.Ordinal).Count() == 3
            && first.Id.StartsWith("item_", StringComparison.Ordinal);

        player.GlobalPosition = mapEvent.GlobalPosition;
        var eventActivated = interaction.TryInteractNearest();
        var eventPriorityPass = eventActivated && mapEvent.IsCompleted && player.Inventory.ItemCount == 0;
        var itemPicked = interaction.TryInteractNearest();
        var pickupPass = itemPicked && player.Inventory.ItemCount == 1;

        var saveError = string.Empty;
        var loadError = string.Empty;
        var savePass = save.TrySaveCurrentRun(out saveError);
        if (savePass)
        {
            savePass = save.TryLoadAndApplyLastRun(out _, out loadError);
        }

        boss.ApplyDamage(new DamageRequest(9999, DamageType.Physical, "stabilization_boss_kill"));
        var terminalPass = flow.State == GameFlowState.MapComplete && GetTree().Paused && rewards.ChoiceActive;
        var rewardPass = rewards.TryChooseReward(1) && rewards.HasChosen;

        player.ApplyDamage(new DamageRequest(9999, DamageType.Physical, "stabilization_dead_player"));
        player.SetEventStats(Stats.Neutral);
        var deathFreezePass = player.CurrentHealth == 0;

        new MinimalSaveService().Delete();
        if (identityPass && eventPriorityPass && pickupPass && savePass && terminalPass && rewardPass && deathFreezePass)
        {
            GD.Print("STABILIZATION_RUN_LOOP_PASS identity=true interaction=true save=true terminal=true reward=true death=true");
            GetTree().Quit(0);
            return;
        }

        GD.PrintErr($"STABILIZATION_RUN_LOOP_FAIL identity={identityPass} interaction={eventPriorityPass} pickup={pickupPass} save={savePass} terminal={terminalPass} reward={rewardPass} death={deathFreezePass} saveError={saveError} loadError={loadError}");
        GetTree().Quit(1);
    }
}
