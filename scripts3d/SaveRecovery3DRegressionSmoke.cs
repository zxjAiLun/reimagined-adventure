using Arpg.Domain;
using Godot;

public partial class SaveRecovery3DRegressionSmoke : Node
{
    private double _elapsed;
    private int _stage;
    private bool _complete;

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_complete)
        {
            return;
        }

        var arena = GetTree().GetFirstNodeInGroup("arena_3d") as TestArena3D;
        var player = arena?.GetNodeOrNull<PlayerController3D>("Player3D");
        var flow = arena?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        var save = arena?.GetNodeOrNull<SaveBoundaryNode3D>("SaveBoundary3D");
        if (player == null || flow == null || save == null)
        {
            if (_elapsed > 6.0)
            {
                Fail("3D recovery nodes did not become ready");
            }

            return;
        }

        switch (_stage)
        {
            case 0:
                player.SetRewardStats(new Stats { DamageMultiplier = 1.2 });
                if (!save.TrySaveCurrentRun(out var saveError))
                {
                    Fail($"could not create Playing save: {saveError}");
                    return;
                }

                player.SetRewardStats(Stats.Neutral);
                save.InjectFailureAfterRewardForTest = true;
                _stage = 1;
                return;

            case 1:
                var rejected = !save.TryLoadAndApplyLastRun(out _, out _);
                var rollbackPass = rejected
                    && player.RewardStats.EquivalentTo(Stats.Neutral)
                    && player.CurrentHealth == player.MaxHealth
                    && flow.State == GameFlowState.Playing;
                save.InjectFailureAfterRewardForTest = false;
                if (!rollbackPass)
                {
                    Fail("failed 3D restore did not roll back atomically");
                    return;
                }

                _stage = 2;
                return;

            case 2:
                player.ApplyDamage(new DamageRequest(
                    9999,
                    DamageType.Physical,
                    "save_recovery_smoke",
                    CombatFaction.Enemy));
                _stage = 3;
                return;

            case 3:
                if (flow.State != GameFlowState.GameOver || player.IsAlive)
                {
                    if (_elapsed > 8.0)
                    {
                        Fail($"death setup failed state={flow.State} alive={player.IsAlive}");
                    }

                    return;
                }

                if (!save.TryLoadAndApplyLastRun(out _, out var loadError))
                {
                    Fail($"Playing save could not restore after death: {loadError}");
                    return;
                }

                var runtimePass = player.IsAlive
                    && player.CurrentHealth > 0
                    && player.CollisionLayer == PlayerController3D.PlayerCollisionLayer
                    && player.CollisionMask == PlayerController3D.PlayerCollisionMask
                    && player.IsPhysicsProcessing()
                    && flow.State == GameFlowState.Playing
                    && !GetTree().Paused
                    && player.RewardStats.DamageMultiplier > 1.0;
                if (!runtimePass)
                {
                    Fail($"runtime restore failed hp={player.CurrentHealth} layer={player.CollisionLayer} mask={player.CollisionMask} physics={player.IsPhysicsProcessing()} state={flow.State} paused={GetTree().Paused}");
                    return;
                }

                _complete = true;
                GD.Print("SAVE_RECOVERY_3D_SPIKE_PASS atomic_rollback=true playing_runtime=true collision_restored=true");
                GetTree().Quit();
                return;
        }

        if (_elapsed > 12.0)
        {
            Fail($"recovery timed out at stage {_stage}");
        }
    }

    private void Fail(string reason)
    {
        _complete = true;
        GD.PushError($"SAVE_RECOVERY_3D_SPIKE_FAIL {reason}");
        GetTree().Quit(1);
    }
}
