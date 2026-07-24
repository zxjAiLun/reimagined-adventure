using Godot;

/// <summary>
/// Converts a viewport mouse position into a point on the playable ground.
/// The player owns the aiming decision; this node only owns camera-ray math.
/// </summary>
public partial class MouseGroundTargeting3D : Node
{
    [Export] public uint GroundCollisionMask { get; set; } = 1u;

    public Camera3D Camera { get; set; }

    public bool TryGetMouseGroundPoint(out Vector3 point)
    {
        return TryGetGroundPoint(GetViewport().GetMousePosition(), out point);
    }

    public bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 point)
    {
        point = Vector3.Zero;
        Camera ??= GetTree().GetFirstNodeInGroup("arena_cameras") as Camera3D;
        var spatialParent = GetParent<Node3D>();
        if (Camera == null || !IsInstanceValid(Camera) || spatialParent.GetWorld3D() == null)
        {
            return false;
        }

        var rayOrigin = Camera.ProjectRayOrigin(screenPosition);
        var rayDirection = Camera.ProjectRayNormal(screenPosition).Normalized();
        var rayEnd = rayOrigin + rayDirection * 2000.0f;
        var query = PhysicsRayQueryParameters3D.Create(
            rayOrigin,
            rayEnd,
            GroundCollisionMask);
        query.CollideWithBodies = true;
        query.CollideWithAreas = false;

        var result = spatialParent.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (result.Count == 0 || !result.TryGetValue("position", out var position))
        {
            return false;
        }

        point = position.AsVector3();
        return true;
    }
}
