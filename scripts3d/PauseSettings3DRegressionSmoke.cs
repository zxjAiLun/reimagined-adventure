using System;
using Godot;

public partial class PauseSettings3DRegressionSmoke : Node
{
    private enum Stage
    {
        StartRun,
        OpenPause,
        ObservePause,
        Resume,
        ReturnToMenu,
        ContinueRun,
        VerifyContinue,
        VerifyTerminalGuard,
    }

    private readonly MinimalSaveService _saveService = new();
    private Stage _stage;
    private double _stageElapsed;
    private bool _complete;
    private GameBootstrap3D _bootstrap;
    private ProductSettingsNode3D _settings;
    private RunSessionNode _run;
    private PauseMenuController3D _pause;
    private PlayerController3D _player;
    private Vector3 _pausedPosition;

    public override void _EnterTree()
    {
        _saveService.Delete();
        var settingsPath = ProjectSettings.GlobalizePath("user://product_settings.cfg");
        if (FileAccess.FileExists("user://product_settings.cfg"))
        {
            DirAccess.RemoveAbsolute(settingsPath);
        }
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _bootstrap = GetNodeOrNull<GameBootstrap3D>("GameBootstrap3D");
        _settings = _bootstrap?.GetNodeOrNull<ProductSettingsNode3D>("ProductSettings3D");
        if (_bootstrap == null || _settings == null)
        {
            Fail("pause/settings smoke could not bind product bootstrap");
        }
    }

    public override void _Process(double delta)
    {
        if (_complete)
        {
            return;
        }

        _stageElapsed += delta;
        if (_stageElapsed > 20.0)
        {
            Fail($"pause/settings smoke timed out at {_stage}");
            return;
        }

        switch (_stage)
        {
            case Stage.StartRun:
                StartRun();
                break;
            case Stage.OpenPause:
                OpenPause();
                break;
            case Stage.ObservePause:
                ObservePause();
                break;
            case Stage.Resume:
                Resume();
                break;
            case Stage.ReturnToMenu:
                ReturnToMenu();
                break;
            case Stage.ContinueRun:
                ContinueRun();
                break;
            case Stage.VerifyContinue:
                VerifyContinue();
                break;
            case Stage.VerifyTerminalGuard:
                VerifyTerminalGuard();
                break;
        }
    }

    private void StartRun()
    {
        if (!_bootstrap.StartNewRun())
        {
            Fail("pause/settings smoke could not start New Run");
            return;
        }
        Advance(Stage.OpenPause);
    }

    private void OpenPause()
    {
        _run ??= _bootstrap.ActiveRun;
        var arena = _run?.CurrentMap3D;
        _pause ??= arena?.GetNodeOrNull<PauseMenuController3D>("PauseMenu3D");
        _player ??= arena?.GetNodeOrNull<PlayerController3D>("Player3D");
        if (_pause == null || _player == null)
        {
            return;
        }

        var controls = _pause.GetNodeOrNull<Label>("PauseRoot/MenuCenter/MenuPanel/Content/Controls")?.Text ?? string.Empty;
        if (!controls.Contains("WASD", StringComparison.Ordinal)
            || !controls.Contains("Esc", StringComparison.Ordinal)
            || !_pause.Open()
            || !GetTree().Paused)
        {
            Fail("pause menu did not expose controls or pause Playing state");
            return;
        }

        _pause.SetVolumeForTest(0.35f);
        _pause.SetFullscreenForTest(true);
        _pausedPosition = _player.GlobalPosition;
        Advance(Stage.ObservePause);
    }

    private void ObservePause()
    {
        if (_stageElapsed < 0.3)
        {
            return;
        }

        var config = new ConfigFile();
        var loadError = config.Load("user://product_settings.cfg");
        if (!GetTree().Paused
            || !_pause.IsOpen
            || _player.GlobalPosition.DistanceTo(_pausedPosition) > 0.001f
            || Math.Abs(_pause.DisplayedVolume - 0.35f) > 0.001f
            || !_pause.DisplayedFullscreen
            || loadError != Error.Ok
            || Math.Abs((float)config.GetValue("audio", "master_volume", 0.0f).AsDouble() - 0.35f) > 0.001f
            || !config.GetValue("display", "fullscreen", false).AsBool())
        {
            Fail("pause freeze or persisted settings contract failed");
            return;
        }

        Advance(Stage.Resume);
    }

    private void Resume()
    {
        _pause.Resume();
        if (_pause.IsOpen || GetTree().Paused)
        {
            Fail("Resume did not restore Playing processing");
            return;
        }

        if (!_pause.Open())
        {
            Fail("pause menu could not reopen before menu return");
            return;
        }
        Advance(Stage.ReturnToMenu);
    }

    private void ReturnToMenu()
    {
        if (!_pause.ReturnToMainMenu())
        {
            Fail("save-and-return action failed");
            return;
        }

        Advance(Stage.ContinueRun);
    }

    private void ContinueRun()
    {
        if (!_bootstrap.IsMenuVisible)
        {
            return;
        }

        if (!_bootstrap.IsContinueEnabled || !_bootstrap.ContinueRun())
        {
            Fail("saved run was not available after returning to menu");
            return;
        }

        _run = null;
        _pause = null;
        _player = null;
        Advance(Stage.VerifyContinue);
    }

    private void VerifyContinue()
    {
        _run ??= _bootstrap.ActiveRun;
        var arena = _run?.CurrentMap3D;
        _pause ??= arena?.GetNodeOrNull<PauseMenuController3D>("PauseMenu3D");
        var flow = arena?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        if (_pause == null || flow == null || _bootstrap.IsMenuVisible)
        {
            return;
        }

        if (flow.State != GameFlowState.Playing
            || Math.Abs(_pause.DisplayedVolume - 0.35f) > 0.001f
            || !_pause.DisplayedFullscreen
            || !flow.RestoreState(GameFlowState.GameOver))
        {
            Fail("continued run did not preserve settings or enter terminal fixture");
            return;
        }

        Advance(Stage.VerifyTerminalGuard);
    }

    private void VerifyTerminalGuard()
    {
        if (_pause.Open() || _pause.IsOpen || !GetTree().Paused)
        {
            Fail("pause menu opened over a terminal flow state");
            return;
        }

        _complete = true;
        GetTree().Paused = false;
        _saveService.Delete();
        _settings.SetFullscreen(false);
        _settings.SetMasterVolume(0.8f);
        _settings.DeleteSettingsForTest();
        GD.Print("PAUSE_SETTINGS_3D_REGRESSION_PASS controls=true freeze=true volume=true fullscreen=true save_return=true continue=true terminal_guard=true");
        GetTree().Quit();
    }

    private void Advance(Stage stage)
    {
        _stage = stage;
        _stageElapsed = 0.0;
    }

    private void Fail(string reason)
    {
        if (_complete)
        {
            return;
        }
        _complete = true;
        GetTree().Paused = false;
        _saveService.Delete();
        _settings?.SetFullscreen(false);
        _settings?.SetMasterVolume(0.8f);
        _settings?.DeleteSettingsForTest();
        GD.PushError($"PAUSE_SETTINGS_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}
