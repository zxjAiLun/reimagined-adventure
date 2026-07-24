using Godot;
using System.Linq;

public partial class RunLoop3DRegressionSmoke : Node
{
    private double _elapsed;
    private bool _complete;

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_complete)
        {
            return;
        }

        var run = GetTree().GetFirstNodeInGroup("run_sessions") as RunSessionNode;
        var arena = GetTree().GetNodesInGroup("arena_3d")
            .OfType<TestArena3D>()
            .LastOrDefault();
        var flow = arena?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        var rewards = arena?.GetNodeOrNull<MapRewardNode3D>("MapRewards3D");
        var boss = arena?.GetNodeOrNull<BrimstoneColossusController3D>("BrimstoneColossus3D");
        var save = arena?.GetNodeOrNull<SaveBoundaryNode3D>("SaveBoundary3D");
        var player = arena?.GetNodeOrNull<PlayerController3D>("Player3D");
        if (run == null || flow == null || rewards == null || boss == null || save == null || player == null)
        {
            if (_elapsed > 8.0)
            {
                Fail($"3D run-loop nodes did not become ready run={run != null} flow={flow != null} rewards={rewards != null} boss={boss != null} save={save != null} player={player != null}");
            }

            return;
        }

        if (flow.State == GameFlowState.MapComplete && !rewards.HasChosen)
        {
            rewards.TryChooseReward(0);
            return;
        }

        if (flow.State == GameFlowState.MapComplete && rewards.HasChosen)
        {
            if (!run.LoadNextMap())
            {
                Fail("3D next-map transition was rejected");
                return;
            }

            return;
        }

        if (run.CurrentMapLevel >= 2 && flow.State == GameFlowState.Playing)
        {
            var rewardPass = player.RewardStats.DamageMultiplier > 1.0;
            var savePass = save.TrySaveCurrentRun(out var saveError)
                && save.TryLoadAndApplyLastRun(out _, out var loadError);
            if (!savePass)
            {
                Fail($"3D save/restore failed: {saveError}");
                return;
            }

            _complete = true;
            GD.Print($"RUN_LOOP_3D_SPIKE_PASS map_complete=true reward={rewardPass} next_map=true save_restore={savePass} level={run.CurrentMapLevel}");
            GetTree().Quit();
            return;
        }

        if (flow.State == GameFlowState.Playing && boss.IsAlive)
        {
            boss.ApplyDamage(new Arpg.Domain.DamageRequest(
                9999,
                Arpg.Domain.DamageType.Physical,
                "run_loop_smoke",
                Arpg.Domain.CombatFaction.Player));
            return;
        }

        if (_elapsed > 12.0)
        {
            Fail($"map={run.CurrentMapLevel} state={flow.State}");
        }
    }

    private void Fail(string reason)
    {
        _complete = true;
        GD.PushError($"RUN_LOOP_3D_SPIKE_FAIL {reason}");
        GetTree().Quit(1);
    }
}
