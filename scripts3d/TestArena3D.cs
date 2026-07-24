using Godot;

public partial class TestArena3D : Node3D
{
    public int MapLevel { get; set; } = 1;

    public override void _Ready()
    {
        AddToGroup("arena_3d");

        // Wire the scene-local camera explicitly. Keeping this adapter
        // explicit makes the spike deterministic even when the editor has
        // not populated group metadata for an instanced scene yet.
        var player = GetNode<PlayerController3D>("Player3D");
        var targeting = player.GetNode<MouseGroundTargeting3D>("MouseGroundTargeting3D");
        targeting.Camera = GetNode<Camera3D>("CameraRig/Camera3D");
    }
}
