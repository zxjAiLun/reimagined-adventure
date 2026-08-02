using Godot;

/// <summary>
/// Presentation-only annular warning. The boss owns the impact geometry; this
/// node only exposes the same locked center and radii for readable feedback.
/// </summary>
public partial class RingTelegraph3D : CombatTelegraph3D
{
    public float InnerRadius { get; private set; }
    public float OuterRadius { get; private set; }
    public Vector3 StartPosition { get; private set; }

    private MeshInstance3D _outerVisual;
    private MeshInstance3D _innerVisual;

    public override void _Ready()
    {
        base._Ready();
        _outerVisual = GetNodeOrNull<MeshInstance3D>("OuterVisual");
        _innerVisual = GetNodeOrNull<MeshInstance3D>("InnerVisual");
        RefreshVisual();
    }

    public void Activate(
        float innerRadius,
        float outerRadius,
        Vector3 worldPosition,
        float duration)
    {
        InnerRadius = Mathf.Max(0.0f, innerRadius);
        OuterRadius = Mathf.Max(InnerRadius + 0.01f, outerRadius);
        StartPosition = new Vector3(worldPosition.X, 0.05f, worldPosition.Z);
        base.Activate(StartPosition, duration);
    }

    protected override void RefreshVisual()
    {
        if (_outerVisual != null)
        {
            _outerVisual.Visible = IsActive;
            _outerVisual.Scale = new Vector3(OuterRadius, 1.0f, OuterRadius);
        }

        if (_innerVisual != null)
        {
            _innerVisual.Visible = IsActive && InnerRadius > 0.0f;
            _innerVisual.Scale = new Vector3(InnerRadius, 1.0f, InnerRadius);
        }
    }
}
