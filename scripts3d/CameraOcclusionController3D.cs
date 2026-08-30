using Godot;

/// <summary>
/// Map-local camera-to-player visibility probe. Occluder collision uses a
/// dedicated layer and never participates in gameplay navigation.
/// </summary>
public partial class CameraOcclusionController3D : Node
{
    [Export(PropertyHint.Layers3DPhysics)] public uint OccluderMask { get; set; } = 32u;

    public int ProbeCount { get; private set; }
    public OccluderVisual3D ActiveOccluder { get; private set; }

    private Camera3D _camera;
    private Node3D _player;

    public override void _Ready()
    {
        _camera = GetParent()?.GetNodeOrNull<Camera3D>("Camera3D");
        var map = GetParent()?.GetParent();
        _player = map?.GetNodeOrNull<Node3D>("Player3D");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_camera == null || _player == null || !IsInstanceValid(_player))
        {
            return;
        }

        ProbeCount++;
        var query = PhysicsRayQueryParameters3D.Create(
            _camera.GlobalPosition,
            _player.GlobalPosition + Vector3.Up * 0.8f,
            OccluderMask);
        query.CollideWithAreas = false;
        var result = _camera.GetWorld3D().DirectSpaceState.IntersectRay(query);
        var next = result.TryGetValue("collider", out var colliderValue)
            ? FindOccluder(colliderValue.AsGodotObject() as Node)
            : null;

        if (ActiveOccluder != null && ActiveOccluder != next && IsInstanceValid(ActiveOccluder))
        {
            ActiveOccluder.SetOccluded(false);
        }
        ActiveOccluder = next;
        ActiveOccluder?.SetOccluded(true);
    }

    public override void _ExitTree()
    {
        if (ActiveOccluder != null && IsInstanceValid(ActiveOccluder))
        {
            ActiveOccluder.SetOccluded(false);
        }
    }

    private static OccluderVisual3D FindOccluder(Node node)
    {
        for (var current = node; current != null; current = current.GetParent())
        {
            if (current is OccluderVisual3D occluder)
            {
                return occluder;
            }
        }
        return null;
    }
}
