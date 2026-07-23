using Godot;

public enum GameFlowState
{
    Playing,
    GameOver,
    MapComplete,
}

/// <summary>
/// Owns only the vertical-slice run state. Actor AI and combat remain
/// independent; this node reacts to the player and Boss death signals.
/// </summary>
public partial class GameFlowController : Node
{
    public GameFlowState State { get; private set; } = GameFlowState.Playing;

    private PlayerController _player;
    private BrimstoneColossusController _boss;
    private Label _overlay;

    public override void _Ready()
    {
        _player = GetNodeOrNull<PlayerController>("../Player");
        _boss = GetNodeOrNull<BrimstoneColossusController>("../BrimstoneColossus");
        _overlay = GetNodeOrNull<Label>("../CanvasLayer/ResultOverlay");

        var playerHealth = _player?.GetNodeOrNull<HealthComponent>("HealthComponent");
        if (playerHealth != null)
        {
            playerHealth.Died += OnPlayerDied;
        }

        var bossHealth = _boss?.GetNodeOrNull<HealthComponent>("HealthComponent");
        if (bossHealth != null)
        {
            bossHealth.Died += OnBossDied;
        }
        SetProcessUnhandledInput(true);
        RefreshOverlay();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (State == GameFlowState.Playing)
        {
            return;
        }

        if (@event.IsActionPressed("restart_run", true))
        {
            RestartRun();
        }
    }

    public void RestartRun()
    {
        GetTree().ReloadCurrentScene();
    }

    private void OnPlayerDied()
    {
        if (State != GameFlowState.Playing)
        {
            return;
        }

        State = GameFlowState.GameOver;
        FreezeGameplay();
        RefreshOverlay();
    }

    private void OnBossDied()
    {
        if (State != GameFlowState.Playing)
        {
            return;
        }

        if (_player != null && !_player.IsAlive)
        {
            OnPlayerDied();
            return;
        }

        State = GameFlowState.MapComplete;
        FreezeGameplay();
        RefreshOverlay();
    }

    private void FreezeGameplay()
    {
        _player?.SetPhysicsProcess(false);
        _player?.GetNodeOrNull<PlayerSkillController>("SkillBarController")?.SetProcess(false);
        _player?.GetNodeOrNull<PlayerSkillController>("SkillBarController")?.SetProcessInput(false);
        _player?.GetNodeOrNull<InventoryController>("InventoryController")?.SetProcessUnhandledInput(false);

        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is Node actor)
            {
                actor.SetPhysicsProcess(false);
            }
        }
    }

    private void RefreshOverlay()
    {
        if (_overlay == null)
        {
            return;
        }

        _overlay.Visible = State != GameFlowState.Playing;
        _overlay.Text = State switch
        {
            GameFlowState.GameOver => "GAME OVER\nPress R to restart",
            GameFlowState.MapComplete => "MAP COMPLETE\nBrimstone Colossus defeated\nPress R to replay",
            _ => string.Empty,
        };
    }
}
