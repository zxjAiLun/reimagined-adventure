using Godot;

public partial class OccluderVisual3D : Node3D
{
    [Export] public NodePath MeshPath { get; set; } = new("Mesh");
    [Export] public float OccludedAlpha { get; set; } = 0.18f;
    [Export] public float FadeSpeed { get; set; } = 8.0f;

    public bool IsOccluded { get; private set; }
    public float CurrentAlpha => _material?.AlbedoColor.A ?? 1.0f;

    private MeshInstance3D _mesh;
    private StandardMaterial3D _material;

    public override void _Ready()
    {
        _mesh = GetNodeOrNull<MeshInstance3D>(MeshPath);
        var source = _mesh?.GetActiveMaterial(0) as StandardMaterial3D;
        if (_mesh == null || source == null)
        {
            return;
        }

        _material = source.Duplicate() as StandardMaterial3D;
        _material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _mesh.MaterialOverride = _material;
        AddToGroup("camera_occluders_3d");
    }

    public override void _Process(double delta)
    {
        if (_material == null)
        {
            return;
        }

        var target = IsOccluded ? Mathf.Clamp(OccludedAlpha, 0.05f, 1.0f) : 1.0f;
        var current = _material.AlbedoColor;
        var blend = 1.0f - Mathf.Exp(-Mathf.Max(0.1f, FadeSpeed) * (float)delta);
        current.A = Mathf.Lerp(current.A, target, blend);
        _material.AlbedoColor = current;
    }

    public void SetOccluded(bool occluded) => IsOccluded = occluded;
}
