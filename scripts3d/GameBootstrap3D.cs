using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// Product entry boundary for starting a fresh run or restoring the last valid
/// save. It owns scene replacement only; run rules remain in RunSessionNode.
/// </summary>
public partial class GameBootstrap3D : Node
{
    [Export] public PackedScene RunShellScene { get; set; }

    private readonly MinimalSaveService _saveService = new();
    private Control _menu;
    private Button _newRunButton;
    private Button _continueButton;
    private Button _quitButton;
    private Label _statusLabel;
    private RunSessionNode _activeRun;
    private bool _restartPending;

    public bool IsMenuVisible => _menu?.Visible == true;
    public bool IsContinueEnabled => _continueButton?.Disabled == false;
    public string StatusText => _statusLabel?.Text ?? string.Empty;
    public RunSessionNode ActiveRun => GodotObject.IsInstanceValid(_activeRun) ? _activeRun : null;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        AddToGroup("game_bootstrap_3d");

        _menu = GetNodeOrNull<Control>("MainMenu");
        _newRunButton = GetNodeOrNull<Button>("MainMenu/MenuCenter/MenuPanel/MenuButtons/NewRun");
        _continueButton = GetNodeOrNull<Button>("MainMenu/MenuCenter/MenuPanel/MenuButtons/Continue");
        _quitButton = GetNodeOrNull<Button>("MainMenu/MenuCenter/MenuPanel/MenuButtons/Quit");
        _statusLabel = GetNodeOrNull<Label>("MainMenu/MenuCenter/MenuPanel/MenuButtons/Status");

        if (_newRunButton != null)
        {
            _newRunButton.Pressed += OnNewRunPressed;
        }
        if (_continueButton != null)
        {
            _continueButton.Pressed += OnContinuePressed;
        }
        if (_quitButton != null)
        {
            _quitButton.Pressed += OnQuitPressed;
        }

        GetTree().Paused = false;
        RefreshSaveAvailability();
        _newRunButton?.GrabFocus();
    }

    public override void _ExitTree()
    {
        if (_newRunButton != null)
        {
            _newRunButton.Pressed -= OnNewRunPressed;
        }
        if (_continueButton != null)
        {
            _continueButton.Pressed -= OnContinuePressed;
        }
        if (_quitButton != null)
        {
            _quitButton.Pressed -= OnQuitPressed;
        }

        // Packed run resources can own managed Godot Array wrappers even when
        // the player exits from the menu before a RunShell is instantiated.
        // Finalize them while the native runtime is still available.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    public void RefreshSaveAvailability()
    {
        var canContinue = _saveService.TryLoad(out _, out var error);
        if (_continueButton != null)
        {
            _continueButton.Disabled = !canContinue;
        }

        SetStatus(canContinue || error == "save file does not exist"
            ? string.Empty
            : $"Save unavailable: {error}");
    }

    public bool StartNewRun()
    {
        if (ActiveRun != null)
        {
            return false;
        }

        _saveService.Delete();
        return StartRun(null, out _);
    }

    public bool ContinueRun()
    {
        if (ActiveRun != null)
        {
            return false;
        }

        if (!_saveService.TryLoad(out var state, out var error))
        {
            SetStatus($"Continue failed: {error}");
            RefreshSaveAvailability();
            return false;
        }

        return StartRun(state, out _);
    }

    public void RestartRun()
    {
        GetTree().Paused = false;
        _saveService.Delete();
        if (ActiveRun != null)
        {
            var previousRun = _activeRun;
            previousRun.ProcessMode = ProcessModeEnum.Disabled;
            previousRun.TreeExited += OnRestartedRunExited;
            _activeRun = null;
            _restartPending = true;
            previousRun.QueueFree();
            return;
        }

        CallDeferred(nameof(StartFreshRunDeferred));
    }

    private bool StartRun(MinimalRunState restoreState, out string error)
    {
        if (RunShellScene == null)
        {
            error = "run shell scene is not configured";
            SetStatus($"Start failed: {error}");
            return false;
        }

        var run = RunShellScene.Instantiate<RunSessionNode>();
        run.Name = "RunShell3D";
        run.ProcessMode = restoreState == null
            ? ProcessModeEnum.Pausable
            : ProcessModeEnum.Disabled;

        if (restoreState != null)
        {
            if (!run.PrepareStartupRestore(restoreState, out error))
            {
                run.Free();
                SetStatus($"Continue failed: {error}");
                return false;
            }

            run.StartupRestoreCompleted += OnStartupRestoreCompleted;
            SetStatus("Loading saved run...");
        }

        _activeRun = run;
        SetMenuBusy(true);
        if (restoreState == null)
        {
            GetTree().Paused = false;
            SetMenuVisible(false);
        }

        AddChild(run);
        error = string.Empty;
        return true;
    }

    private void OnStartupRestoreCompleted(bool succeeded, string error)
    {
        var run = _activeRun;
        if (run != null && GodotObject.IsInstanceValid(run))
        {
            run.StartupRestoreCompleted -= OnStartupRestoreCompleted;
        }

        if (succeeded)
        {
            run.ProcessMode = ProcessModeEnum.Pausable;
            SetStatus(string.Empty);
            SetMenuVisible(false);
            return;
        }

        GetTree().Paused = false;
        if (run != null && GodotObject.IsInstanceValid(run))
        {
            run.QueueFree();
        }
        _activeRun = null;
        SetMenuBusy(false);
        SetMenuVisible(true);
        SetStatus($"Continue failed: {error}");
    }

    private void StartFreshRunDeferred()
    {
        if (ActiveRun == null)
        {
            _restartPending = false;
            StartRun(null, out _);
        }
    }

    private void OnRestartedRunExited()
    {
        if (_restartPending)
        {
            CallDeferred(nameof(StartFreshRunDeferred));
        }
    }

    private void OnNewRunPressed() => StartNewRun();

    private void OnContinuePressed() => ContinueRun();

    private void OnQuitPressed() => GetTree().Quit();

    private void SetMenuVisible(bool visible)
    {
        if (_menu != null)
        {
            _menu.Visible = visible;
        }
    }

    private void SetMenuBusy(bool busy)
    {
        if (_newRunButton != null)
        {
            _newRunButton.Disabled = busy;
        }
        if (_continueButton != null)
        {
            _continueButton.Disabled = busy || !_saveService.TryLoad(out _, out _);
        }
    }

    private void SetStatus(string text)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = text ?? string.Empty;
        }
    }
}
