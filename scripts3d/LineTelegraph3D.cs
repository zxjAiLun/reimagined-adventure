using Godot;

/// <summary>Locked horizontal direction warning marker.</summary>
public partial class LineTelegraph3D : CombatTelegraph3D
{
    [Export] public float Length { get; private set; } = 1.0f;
    public Vector3 LockedDirection { get; private set; } = Vector3.Forward;
    public Vector3 StartPosition { get; private set; }
    public Vector3 EndPosition { get; private set; }

    private MeshInstance3D _visual;

    public override void _Ready()
    {
        base._Ready();
        _visual = GetNodeOrNull<MeshInstance3D>("Visual");
        RefreshVisual();
    }

    public void Activate(
        Vector3 worldPosition,
        Vector3 direction,
        float length,
        float duration)
    {
        direction.Y = 0.0f;
        LockedDirection = direction.LengthSquared() > 0.001f
            ? direction.Normalized()
            : Vector3.Forward;
        Length = Mathf.Max(0.1f, length);
        StartPosition = new Vector3(worldPosition.X, worldPosition.Y + 0.08f, worldPosition.Z);
        EndPosition = StartPosition + LockedDirection * Length;
        var center = StartPosition + LockedDirection * (Length * 0.5f);
        base.Activate(center, duration);
        LookAt(center + LockedDirection, Vector3.Up);
    }

    protected override void RefreshVisual()
    {
        if (_visual == null)
        {
            return;
        }

        _visual.Visible = IsActive;
        _visual.Scale = new Vector3(1.0f, 1.0f, Length);
    }
}
