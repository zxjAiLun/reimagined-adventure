using Godot;

/// <summary>Flat circular warning marker. It only displays an attack area.</summary>
public partial class AreaTelegraph3D : CombatTelegraph3D
{
    [Export] public float Radius { get; private set; } = 1.0f;

    private MeshInstance3D _visual;

    public override void _Ready()
    {
        base._Ready();
        _visual = GetNodeOrNull<MeshInstance3D>("Visual");
        RefreshVisual();
    }

    public void Activate(float radius, Vector3 worldPosition, float duration)
    {
        Radius = Mathf.Max(0.01f, radius);
        base.Activate(new Vector3(worldPosition.X, 0.05f, worldPosition.Z), duration);
    }

    protected override void RefreshVisual()
    {
        if (_visual == null)
        {
            return;
        }

        _visual.Visible = IsActive;
        _visual.Scale = new Vector3(Radius, 1.0f, Radius);
    }
}
