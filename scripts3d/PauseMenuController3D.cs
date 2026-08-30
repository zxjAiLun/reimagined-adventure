using Godot;

/// <summary>
/// Pause presentation and input boundary. It delegates persistence to the
/// map SaveBoundary and application settings to ProductSettingsNode3D.
/// </summary>
public partial class PauseMenuController3D : CanvasLayer
{
    private Control _panel;
    private Button _resumeButton;
    private Button _mainMenuButton;
    private CheckButton _fullscreenToggle;
    private HSlider _volumeSlider;
    private Label _volumeValue;
    private GameFlowController3D _flow;
    private SaveBoundaryNode3D _save;
    private ProductSettingsNode3D _settings;
    private bool _syncing;

    public bool IsOpen => _panel?.Visible == true;
    public float DisplayedVolume => _volumeSlider == null ? 0.0f : (float)_volumeSlider.Value;
    public bool DisplayedFullscreen => _fullscreenToggle?.ButtonPressed == true;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _panel = GetNodeOrNull<Control>("PauseRoot");
        _resumeButton = GetNodeOrNull<Button>("PauseRoot/MenuCenter/MenuPanel/Content/Resume");
        _mainMenuButton = GetNodeOrNull<Button>("PauseRoot/MenuCenter/MenuPanel/Content/MainMenu");
        _fullscreenToggle = GetNodeOrNull<CheckButton>("PauseRoot/MenuCenter/MenuPanel/Content/Fullscreen");
        _volumeSlider = GetNodeOrNull<HSlider>("PauseRoot/MenuCenter/MenuPanel/Content/VolumeRow/Volume");
        _volumeValue = GetNodeOrNull<Label>("PauseRoot/MenuCenter/MenuPanel/Content/VolumeRow/Value");
        _flow = GetNodeOrNull<GameFlowController3D>("../GameFlow3D");
        _save = GetNodeOrNull<SaveBoundaryNode3D>("../SaveBoundary3D");
        _settings = FindSettings();

        if (_resumeButton != null)
        {
            _resumeButton.Pressed += Resume;
        }
        if (_mainMenuButton != null)
        {
            _mainMenuButton.Pressed += OnMainMenuPressed;
        }
        if (_fullscreenToggle != null)
        {
            _fullscreenToggle.Toggled += OnFullscreenToggled;
        }
        if (_volumeSlider != null)
        {
            _volumeSlider.ValueChanged += OnVolumeChanged;
        }
        if (_settings != null)
        {
            _settings.SettingsChanged += OnSettingsChanged;
        }

        SetOpen(false);
        SyncSettings();
    }

    public override void _ExitTree()
    {
        if (_resumeButton != null)
        {
            _resumeButton.Pressed -= Resume;
        }
        if (_mainMenuButton != null)
        {
            _mainMenuButton.Pressed -= OnMainMenuPressed;
        }
        if (_fullscreenToggle != null)
        {
            _fullscreenToggle.Toggled -= OnFullscreenToggled;
        }
        if (_volumeSlider != null)
        {
            _volumeSlider.ValueChanged -= OnVolumeChanged;
        }
        if (_settings != null && GodotObject.IsInstanceValid(_settings))
        {
            _settings.SettingsChanged -= OnSettingsChanged;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("pause_game", true))
        {
            return;
        }

        if (IsOpen)
        {
            Resume();
        }
        else
        {
            Open();
        }

        GetViewport().SetInputAsHandled();
    }

    public bool Open()
    {
        if (_flow?.State != GameFlowState.Playing || IsOpen)
        {
            return false;
        }

        GetTree().Paused = true;
        SetOpen(true);
        SyncSettings();
        _resumeButton?.GrabFocus();
        return true;
    }

    public void Resume()
    {
        if (!IsOpen)
        {
            return;
        }

        SetOpen(false);
        if (_flow?.State == GameFlowState.Playing)
        {
            GetTree().Paused = false;
        }
    }

    public bool ReturnToMainMenu()
    {
        if (!IsOpen)
        {
            return false;
        }

        var error = string.Empty;
        if (_save == null || !_save.TrySaveCurrentRun(out error))
        {
            GD.PushError($"Could not save before returning to menu: {error}");
            return false;
        }

        var bootstrap = MapRuntimeScope3D.FindRunSession(this)?.GetParent() as GameBootstrap3D;
        if (bootstrap == null)
        {
            return false;
        }

        SetOpen(false);
        bootstrap.ReturnToMenu();
        return true;
    }

    public void SetVolumeForTest(float value) => _settings?.SetMasterVolume(value);

    public void SetFullscreenForTest(bool enabled) => _settings?.SetFullscreen(enabled);

    private void OnMainMenuPressed() => ReturnToMainMenu();

    private ProductSettingsNode3D FindSettings()
    {
        for (var current = GetParent(); current != null; current = current.GetParent())
        {
            var settings = current.GetNodeOrNull<ProductSettingsNode3D>("ProductSettings3D");
            if (settings != null)
            {
                return settings;
            }
        }

        return GetTree().GetFirstNodeInGroup("product_settings_3d") as ProductSettingsNode3D;
    }

    private void OnSettingsChanged(float volume, bool fullscreen) => SyncSettings();

    private void OnFullscreenToggled(bool enabled)
    {
        if (!_syncing)
        {
            _settings?.SetFullscreen(enabled);
        }
    }

    private void OnVolumeChanged(double value)
    {
        if (!_syncing)
        {
            _settings?.SetMasterVolume((float)value);
        }
        RefreshVolumeLabel();
    }

    private void SyncSettings()
    {
        if (_settings == null)
        {
            return;
        }

        _syncing = true;
        if (_fullscreenToggle != null)
        {
            _fullscreenToggle.ButtonPressed = _settings.Fullscreen;
        }
        if (_volumeSlider != null)
        {
            _volumeSlider.Value = _settings.MasterVolume;
        }
        _syncing = false;
        RefreshVolumeLabel();
    }

    private void RefreshVolumeLabel()
    {
        if (_volumeValue != null && _volumeSlider != null)
        {
            _volumeValue.Text = $"{Mathf.RoundToInt((float)_volumeSlider.Value * 100.0f)}%";
        }
    }

    private void SetOpen(bool open)
    {
        if (_panel != null)
        {
            _panel.Visible = open;
        }
    }
}
