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
    private EncounterDirector3D _encounterDirector;
    private MapRewardNode3D _mapRewards;
    private AtlasRouteChoiceController3D _routeChoice;
    private RunSessionNode _runSession;
    private Label _overlay;
    private bool _playerBound;
    private bool _bossBound;
    private bool _encounterBound;
    private bool _exiting;
    private int _bindAttempts;

    public override void _Ready()
    {
        _exiting = false;
        _bindAttempts = 0;
        ProcessMode = ProcessModeEnum.Always;
        AddToGroup("game_flows_3d");
        SetProcessUnhandledInput(true);
        CallDeferred(nameof(BindRuntimeNodes));
        RefreshOverlay();
    }

    public override void _ExitTree()
    {
        _exiting = true;
        if (_playerBound)
        {
            var playerHealth = _player?.GetNodeOrNull<HealthComponent>("HealthComponent");
            if (playerHealth != null)
            {
                playerHealth.Died -= OnPlayerDied;
            }
        }

        if (_bossBound)
        {
            var bossHealth = _boss?.GetNodeOrNull<HealthComponent>("HealthComponent");
            if (bossHealth != null)
            {
                bossHealth.Died -= OnBossDied;
            }
        }

        if (_encounterBound && GodotObject.IsInstanceValid(_encounterDirector))
        {
            _encounterDirector.EncounterCompleted -= OnEncounterCompleted;
        }
    }

    private void BindRuntimeNodes()
    {
        if (_exiting || !IsInsideTree())
        {
            return;
        }

        _bindAttempts++;
        if (_player == null)
        {
            _player = GetNodeOrNull<PlayerController3D>("../Player3D");
        }

        if (_mapRewards == null)
        {
            _mapRewards = GetNodeOrNull<MapRewardNode3D>("../MapRewards3D");
        }

        if (_routeChoice == null)
        {
            _routeChoice = GetNodeOrNull<AtlasRouteChoiceController3D>("../AtlasRouteChoice3D");
        }

        if (_runSession == null)
        {
            var map = GetParent<Node3D>();
            _runSession = map?.GetParent() as RunSessionNode
                ?? map?.GetNodeOrNull<RunSessionNode>("RunSession");
        }

        if (_overlay == null)
        {
            _overlay = GetNodeOrNull<Label>("../HUD/ResultOverlay");
        }

        if (_encounterDirector == null)
        {
            _encounterDirector = GetNodeOrNull<EncounterDirector3D>("../EncounterDirector3D");
        }

        var playerHealth = _player?.GetNodeOrNull<HealthComponent>("HealthComponent");
        if (!_playerBound && playerHealth != null)
        {
            playerHealth.Died += OnPlayerDied;
            _playerBound = true;
        }

        if (_encounterDirector?.IsOperational == true)
        {
            if (!_encounterBound)
            {
                _encounterDirector.EncounterCompleted += OnEncounterCompleted;
                _encounterBound = true;
            }
        }
        else
        {
            _boss ??= GetNodeOrNull<BrimstoneColossusController3D>("../BrimstoneColossus3D");
            var bossHealth = _boss?.GetNodeOrNull<HealthComponent>("HealthComponent");
            if (!_bossBound && bossHealth != null)
            {
                bossHealth.Died += OnBossDied;
                _bossBound = true;
            }
        }

        var legacyBossExists = GetNodeOrNull<BrimstoneColossusController3D>("../BrimstoneColossus3D") != null;
        var needsRetry = !_playerBound
            || (_encounterDirector?.IsOperational != true && legacyBossExists && !_bossBound);
        if (needsRetry && _bindAttempts < 60)
        {
            CallDeferred(nameof(BindRuntimeNodes));
        }
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
            var loaded = _routeChoice != null
                ? _routeChoice.TryConfirm()
                : _runSession?.LoadSelectedMap() == true;
            if (loaded)
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
        CompleteMap();
    }

    private void OnEncounterCompleted()
    {
        _runSession?.TryCompleteCurrentAtlasMap();
        CompleteMap();
    }

    private void CompleteMap()
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
