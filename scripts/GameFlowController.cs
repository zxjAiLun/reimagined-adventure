using System;
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
    [Export] public string AtlasMapId { get; set; } = "quiet-coast";

    public GameFlowState State { get; private set; } = GameFlowState.Playing;

    private PlayerController _player;
    private BrimstoneColossusController _boss;
    private AtlasNode _atlas;
    private MapRewardNode _mapRewards;
    private RunSessionNode _runSession;
    private Label _overlay;

    public override void _Ready()
    {
        ProcessMode = Node.ProcessModeEnum.Always;
        _player = GetNodeOrNull<PlayerController>("../Player");
        _boss = GetNodeOrNull<BrimstoneColossusController>("../BrimstoneColossus");
        _atlas = GetNodeOrNull<AtlasNode>("../Atlas");
        _mapRewards = GetNodeOrNull<MapRewardNode>("../MapRewards");
        _runSession = GetTree().GetFirstNodeInGroup("run_sessions") as RunSessionNode;
        _overlay = GetNodeOrNull<Label>("../CanvasLayer/ResultOverlay");
        AddToGroup("game_flows");

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

        if (State == GameFlowState.MapComplete
            && @event.IsActionPressed("next_map", true)
            && _mapRewards?.HasChosen == true)
        {
            if (_runSession?.LoadNextMap() == true)
            {
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (@event.IsActionPressed("restart_run", true))
        {
            RestartRun();
        }
    }

    public void RestartRun()
    {
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }

    public bool RestoreState(GameFlowState state)
    {
        if (!Enum.IsDefined(state))
        {
            return false;
        }

        State = state;
        if (State != GameFlowState.Playing)
        {
            FreezeGameplay();
        }
        else
        {
            GetTree().Paused = false;
        }

        RefreshOverlay();
        return true;
    }

    public bool PrepareNextMap()
    {
        if (State != GameFlowState.MapComplete)
        {
            return false;
        }

        State = GameFlowState.Playing;
        GetTree().Paused = false;
        RefreshOverlay();
        return true;
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

        _atlas?.TryCompleteMap(AtlasMapId);
        State = GameFlowState.MapComplete;
        _mapRewards?.BeginChoice();
        FreezeGameplay();
        RefreshOverlay();
    }

    private void FreezeGameplay()
    {
        GetTree().Paused = true;
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
            GameFlowState.MapComplete => _mapRewards?.ChoiceText
                ?? "MAP COMPLETE\nBrimstone Colossus defeated\nPress R to replay",
            _ => string.Empty,
        };
    }
}
