using Godot;

/// <summary>
/// Reusable, local hit feedback. A duplicated MaterialOverride isolates each
/// actor from shared scene resources; the actor's combat material is never
/// replaced or mutated.
/// </summary>
public partial class HitFlash3D : Node
{
    [Export] public NodePath MeshPath { get; set; } = new("../Mesh");
    [Export] public float DurationSeconds { get; set; } = 0.12f;
    [Export] public Color FlashColor { get; set; } = new(1.0f, 0.95f, 0.82f, 1.0f);

    public int TriggerCount { get; private set; }
    public bool IsActive => _remainingSeconds > 0.0f;
    public float Progress => DurationSeconds <= 0.0f
        ? 1.0f
        : Mathf.Clamp(1.0f - _remainingSeconds / DurationSeconds, 0.0f, 1.0f);
    public bool HasIsolatedMaterial => _materialOverride != null;

    private MeshInstance3D _mesh;
    private StandardMaterial3D _materialOverride;
    private Color _baseAlbedo;
    private float _remainingSeconds;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        _mesh = GetNodeOrNull<MeshInstance3D>(MeshPath);
        if (_mesh == null)
        {
            return;
        }

        var sourceMaterial = _mesh.GetActiveMaterial(0) as StandardMaterial3D;
        _materialOverride = sourceMaterial?.Duplicate() as StandardMaterial3D
            ?? new StandardMaterial3D();
        _baseAlbedo = _materialOverride.AlbedoColor;
        _mesh.MaterialOverride = _materialOverride;
    }

    public void Trigger()
    {
        TriggerCount++;
        _remainingSeconds = Mathf.Max(0.01f, DurationSeconds);
        ApplyIntensity(1.0f);
    }

    public void ResetPresentation()
    {
        _remainingSeconds = 0.0f;
        ApplyIntensity(0.0f);
    }

    public override void _Process(double delta)
    {
        if (!IsActive)
        {
            return;
        }

        _remainingSeconds = Mathf.Max(0.0f, _remainingSeconds - (float)delta);
        ApplyIntensity(1.0f - Progress);
    }

    private void ApplyIntensity(float intensity)
    {
        if (_materialOverride == null)
        {
            return;
        }

        intensity = Mathf.Clamp(intensity, 0.0f, 1.0f);
        _materialOverride.AlbedoColor = new Color(
            Mathf.Lerp(_baseAlbedo.R, FlashColor.R, intensity),
            Mathf.Lerp(_baseAlbedo.G, FlashColor.G, intensity),
            Mathf.Lerp(_baseAlbedo.B, FlashColor.B, intensity),
            _baseAlbedo.A);
    }
}
