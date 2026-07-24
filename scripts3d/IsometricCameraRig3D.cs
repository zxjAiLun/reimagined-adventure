using Godot;

/// <summary>
/// Fixed-yaw, fixed-pitch camera rig that follows the player on the XZ plane.
/// </summary>
public partial class IsometricCameraRig3D : Node3D
{
    [Export] public Vector3 FollowOffset { get; set; } = new(0.0f, 14.0f, 14.0f);
    [Export] public float FollowSharpness { get; set; } = 12.0f;

    private Node3D _target;

    public override void _Ready()
    {
        FindTarget();
    }

    public override void _Process(double delta)
    {
        if (_target == null || !IsInstanceValid(_target))
        {
            FindTarget();
            return;
        }

        var desired = _target.GlobalPosition + FollowOffset;
        var blend = 1.0f - Mathf.Exp(-FollowSharpness * (float)delta);
        GlobalPosition = GlobalPosition.Lerp(desired, blend);
    }

    private void FindTarget()
    {
        _target = GetTree().GetFirstNodeInGroup("player_3d") as Node3D;
    }
}
