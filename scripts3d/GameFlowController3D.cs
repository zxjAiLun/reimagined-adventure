using System;
using Godot;

/// <summary>
/// 3D run-flow adapter. It only observes actor death and owns the paused
/// result states; combat and actor AI remain in their own nodes.
/// </summary>
public partial class GameFlowController3D : Node
{
    [Signal]
    public delegate void StateChangedEventHandler(int state);

    [Export] public string AtlasMapId { get; set; } = "quiet-coast-3d";

    public GameFlowState State { get; private set; } = GameFlowState.Playing;

    private PlayerController3D _player;
    private BrimstoneColossusController3D _boss;
    private MapRewardNode3D _mapRewards;
    private RunSessionNode _runSession;
    private Label _overlay;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _player = GetNodeOrNull<PlayerController3D>("../Player3D");
        _boss = GetNodeOrNull<BrimstoneColossusController3D>("../BrimstoneColossus3D");
        _mapRewards = GetNodeOrNull<MapRewardNode3D>("../MapRewards3D");
        _runSession = GetTree().GetFirstNodeInGroup("run_sessions") as RunSessionNode;
        _overlay = GetNodeOrNull<Label>("../HUD/ResultOverlay");
        AddToGroup("game_flows_3d");

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
        EmitSignal(SignalName.StateChanged, (int)State);
        if (State == GameFlowState.Playing)
        {
            GetTree().Paused = false;
        }
        else
        {
            GetTree().Paused = true;
        }

        RefreshOverlay();
        return true;
    }

    public bool PrepareNextMap()
    {
        if (State != GameFlowState.MapComplete || _mapRewards?.HasChosen != true)
        {
            return false;
        }

        State = GameFlowState.Playing;
        EmitSignal(SignalName.StateChanged, (int)State);
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
        EmitSignal(SignalName.StateChanged, (int)State);
        FreezeGameplay();
        RefreshOverlay();
    }

    private void OnBossDied()
    {
        if (State != GameFlowState.Playing || _player == null || !_player.IsAlive)
        {
            return;
        }

        State = GameFlowState.MapComplete;
        EmitSignal(SignalName.StateChanged, (int)State);
        _mapRewards?.BeginChoice();
        FreezeGameplay();
        RefreshOverlay();
    }

    private void FreezeGameplay() => GetTree().Paused = true;

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
                ?? "MAP COMPLETE\nChoose reward: 1 / 2 / 3",
            _ => string.Empty,
        };
    }
}
