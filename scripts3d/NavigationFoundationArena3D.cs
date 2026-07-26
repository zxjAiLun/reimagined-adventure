using Godot;

/// <summary>
/// Dedicated Stage 3A planar navigation arena. This is separate from the
/// Greybox stress scene, whose navigation node remains a structure-only
/// pressure fixture.
/// </summary>
public partial class NavigationFoundationArena3D : Node3D
{
    public Aabb ObstacleA => new(
        new Vector3(-1.2f, 0.0f, -4.2f),
        new Vector3(2.4f, 2.0f, 8.4f));

    public Aabb ObstacleB => new(
        new Vector3(1.2f, 0.0f, -6.7f),
        new Vector3(4.6f, 2.0f, 2.4f));

    public override void _Ready()
    {
        AddToGroup("navigation_foundation_arenas_3d");
        var player = GetNodeOrNull<PlayerController3D>("Player3D");
        var targeting = player?.GetNodeOrNull<MouseGroundTargeting3D>("MouseGroundTargeting3D");
        var camera = GetNodeOrNull<Camera3D>("CameraRig/Camera3D");
        if (targeting != null && camera != null)
        {
            targeting.Camera = camera;
        }
    }
}
