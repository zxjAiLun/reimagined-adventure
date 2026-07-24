using Godot;

public partial class GameOver3DRegressionSmoke : Node
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

        var player = GetTree().GetFirstNodeInGroup("player_3d") as PlayerController3D;
        var flow = GetTree().GetFirstNodeInGroup("game_flows_3d") as GameFlowController3D;
        if (player == null || flow == null)
        {
            if (_elapsed > 6.0)
            {
                Fail("3D GameOver nodes did not become ready");
            }

            return;
        }

        if (flow.State == GameFlowState.Playing && player.IsAlive)
        {
            player.ApplyDamage(new Arpg.Domain.DamageRequest(
                9999,
                Arpg.Domain.DamageType.Physical,
                "game_over_smoke",
                Arpg.Domain.CombatFaction.Enemy));
            return;
        }

        if (flow.State == GameFlowState.GameOver && GetTree().Paused)
        {
            _complete = true;
            GD.Print("GAME_OVER_3D_SPIKE_PASS state=true paused=true");
            GetTree().Quit();
            return;
        }

        if (_elapsed > 8.0)
        {
            Fail($"state={flow.State} paused={GetTree().Paused}");
        }
    }

    private void Fail(string reason)
    {
        _complete = true;
        GD.PushError($"GAME_OVER_3D_SPIKE_FAIL {reason}");
        GetTree().Quit(1);
    }
}
