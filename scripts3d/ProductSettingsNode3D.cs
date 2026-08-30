using Godot;

/// <summary>
/// Application-level presentation settings. Gameplay rules never depend on
/// these values; the node only owns validated persistence and engine adapters.
/// </summary>
public partial class ProductSettingsNode3D : Node
{
    [Signal]
    public delegate void SettingsChangedEventHandler(float masterVolume, bool fullscreen);

    [Export] public string SettingsPath { get; set; } = "user://product_settings.cfg";

    public float MasterVolume { get; private set; } = 0.8f;
    public bool Fullscreen { get; private set; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        AddToGroup("product_settings_3d");
        LoadAndApply();
    }

    public void SetMasterVolume(float value)
    {
        var next = Mathf.Clamp(value, 0.0f, 1.0f);
        if (Mathf.IsEqualApprox(next, MasterVolume))
        {
            return;
        }

        MasterVolume = next;
        ApplyAudio();
        Save();
        EmitChanged();
    }

    public void SetFullscreen(bool enabled)
    {
        if (Fullscreen == enabled)
        {
            return;
        }

        Fullscreen = enabled;
        ApplyDisplay();
        Save();
        EmitChanged();
    }

    public void LoadAndApply()
    {
        var config = new ConfigFile();
        if (config.Load(SettingsPath) == Error.Ok)
        {
            MasterVolume = ReadVolume(config);
            Fullscreen = config.GetValue("display", "fullscreen", false).AsBool();
        }

        ApplyAudio();
        ApplyDisplay();
        EmitChanged();
    }

    public void DeleteSettingsForTest()
    {
        var absolute = ProjectSettings.GlobalizePath(SettingsPath);
        if (FileAccess.FileExists(SettingsPath))
        {
            DirAccess.RemoveAbsolute(absolute);
        }
    }

    private static float ReadVolume(ConfigFile config)
    {
        var value = (float)config.GetValue("audio", "master_volume", 0.8f).AsDouble();
        return float.IsFinite(value) ? Mathf.Clamp(value, 0.0f, 1.0f) : 0.8f;
    }

    private void ApplyAudio()
    {
        var masterIndex = AudioServer.GetBusIndex("Master");
        if (masterIndex < 0)
        {
            return;
        }

        AudioServer.SetBusMute(masterIndex, MasterVolume <= 0.0001f);
        AudioServer.SetBusVolumeDb(
            masterIndex,
            MasterVolume <= 0.0001f ? -80.0f : Mathf.LinearToDb(MasterVolume));
    }

    private void ApplyDisplay()
    {
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }

        DisplayServer.WindowSetMode(Fullscreen
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
    }

    private void Save()
    {
        var config = new ConfigFile();
        config.SetValue("audio", "master_volume", MasterVolume);
        config.SetValue("display", "fullscreen", Fullscreen);
        var error = config.Save(SettingsPath);
        if (error != Error.Ok)
        {
            GD.PushError($"Could not save product settings: {error}");
        }
    }

    private void EmitChanged() =>
        EmitSignal(SignalName.SettingsChanged, MasterVolume, Fullscreen);
}
