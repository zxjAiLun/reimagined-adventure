using Godot;

/// <summary>
/// Converts the cursor to a ground point and exposes the resulting aim to the
/// player facade. It contains no skill or movement logic.
/// </summary>
public partial class PlayerAim3D : Node
{
    private PlayerController3D _player;
    private MouseGroundTargeting3D _targeting;

    public Vector3 GroundPoint { get; private set; }
    public bool HasGroundPoint { get; private set; }

    public override void _Ready()
    {
        _player = GetParent<PlayerController3D>();
        _targeting = _player.GetNode<MouseGroundTargeting3D>("MouseGroundTargeting3D");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null || !_player.IsAlive || _targeting == null)
        {
            return;
        }

        HasGroundPoint = _targeting.TryGetMouseGroundPoint(out var point);
        if (!HasGroundPoint)
        {
            return;
        }

        GroundPoint = point;
        var direction = GroundPoint - _player.GlobalPosition;
        direction.Y = 0.0f;
        if (direction.LengthSquared() > 0.001f)
        {
            _player.SetAimDirectionForTest(direction);
        }
    }

    public bool TryGetGroundPoint(out Vector3 point)
    {
        point = GroundPoint;
        return HasGroundPoint;
    }

    public void SetGroundPointForTest(Vector3 point)
    {
        GroundPoint = point;
        HasGroundPoint = true;
    }
}
