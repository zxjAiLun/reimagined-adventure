using Godot;

/// <summary>
/// Owns only XZ movement and collision-aware dash motion for the 3D player.
/// </summary>
public partial class PlayerMotor3D : Node
{
    [Export] public float MoveSpeed { get; set; } = 5.0f;
    [Export] public Vector2 MovementBounds { get; set; } = new(11.0f, 7.0f);

    private PlayerController3D _player;

    public override void _Ready()
    {
        _player = GetParent<PlayerController3D>();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null || !_player.IsAlive)
        {
            return;
        }

        var movement = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        if (movement.LengthSquared() > 1.0f)
        {
            movement = movement.Normalized();
        }

        _player.Velocity = new Vector3(
            movement.X,
            0.0f,
            movement.Y)
            * MoveSpeed
            * (float)_player.EffectiveStats.MoveSpeedMultiplier;
        _player.MoveAndSlide();
        ClampToArena();
    }

    public bool PerformDash(double distance)
    {
        if (_player == null || !_player.IsAlive || distance <= 0.0)
        {
            return false;
        }

        var before = _player.GlobalPosition;
        var motion = _player.AimDirection * (float)distance;
        _player.Velocity = motion;
        _player.MoveAndCollide(motion);
        ClampToArena();
        _player.Velocity = Vector3.Zero;
        return _player.GlobalPosition.DistanceSquaredTo(before) > 0.0001f;
    }

    public void ClampToArena()
    {
        if (_player == null)
        {
            return;
        }

        var position = _player.GlobalPosition;
        position.X = Mathf.Clamp(position.X, -MovementBounds.X, MovementBounds.X);
        position.Z = Mathf.Clamp(position.Z, -MovementBounds.Y, MovementBounds.Y);
        position.Y = 0.0f;
        _player.GlobalPosition = position;
    }
}
