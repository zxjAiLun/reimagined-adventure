using Arpg.Domain;
using Godot;

public partial class Milestone6GameOverSmoke : Node2D
{
    private static int _phase;

    private GameFlowController _flow;
    private PlayerController _player;

    public override void _Ready()
    {
        _flow = GetNode<GameFlowController>("GameFlowController");
        _player = GetNode<PlayerController>("Player");
        GetTree().CreateTimer(0.10).Timeout += _phase == 0 ? KillPlayer : CheckRestart;
    }

    private void KillPlayer()
    {
        _player.ApplyDamage(new DamageRequest(1000, DamageType.Physical, "game_over_smoke"));
        GetTree().CreateTimer(0.10).Timeout += CheckGameOver;
    }

    private void CheckGameOver()
    {
        if (_flow.State != GameFlowState.GameOver)
        {
            GD.PrintErr($"MILESTONE6_GAME_OVER_FAIL state={_flow.State}");
            GetTree().Quit(1);
            return;
        }

        _phase = 1;
        _flow.RestartRun();
    }

    private void CheckRestart()
    {
        if (_flow.State != GameFlowState.Playing || !_player.IsAlive)
        {
            GD.PrintErr($"MILESTONE6_RESTART_FAIL state={_flow.State} alive={_player.IsAlive}");
            GetTree().Quit(1);
            return;
        }

        GD.Print("MILESTONE6_GAME_OVER_RESTART_PASS");
        GetTree().Quit(0);
    }
}
