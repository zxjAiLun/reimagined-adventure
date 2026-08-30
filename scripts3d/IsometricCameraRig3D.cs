using Godot;

/// <summary>
/// Fixed-yaw, fixed-pitch camera rig that follows the player on the XZ plane.
/// </summary>
public partial class IsometricCameraRig3D : Node3D
{
    [Export] public Vector3 FollowOffset { get; set; } = new(0.0f, 14.0f, 14.0f);
    [Export] public float FollowSharpness { get; set; } = 12.0f;
    [Export] public float ShakeDurationSeconds { get; set; } = 0.16f;
    [Export] public float MaximumShakeOffset { get; set; } = 0.45f;

    public int ShakeRequestCount { get; private set; }
    public bool IsShaking => _shakeRemaining > 0.0f;
    public Vector3 ShakeOffset { get; private set; }

    private Node3D _target;
    private Vector3 _followPosition;
    private float _shakeRemaining;
    private float _shakeStrength;
    private float _shakeClock;

    public override void _Ready()
    {
        FindTarget();
        _followPosition = GlobalPosition;
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
        _followPosition = _followPosition.Lerp(desired, blend);
        UpdateShake((float)delta);
        GlobalPosition = _followPosition + ShakeOffset;
    }

    public void RequestShake(float strength)
    {
        if (strength <= 0.0f)
        {
            return;
        }

        ShakeRequestCount++;
        _shakeStrength = Mathf.Clamp(Mathf.Max(_shakeStrength, strength), 0.0f, MaximumShakeOffset);
        _shakeRemaining = Mathf.Max(_shakeRemaining, ShakeDurationSeconds);
    }

    private void UpdateShake(float delta)
    {
        if (_shakeRemaining <= 0.0f)
        {
            ShakeOffset = Vector3.Zero;
            _shakeStrength = 0.0f;
            return;
        }

        _shakeRemaining = Mathf.Max(0.0f, _shakeRemaining - delta);
        _shakeClock += delta;
        var progress = ShakeDurationSeconds <= 0.0f
            ? 0.0f
            : _shakeRemaining / ShakeDurationSeconds;
        var amplitude = _shakeStrength * progress;
        ShakeOffset = new Vector3(
            Mathf.Sin(_shakeClock * 91.0f) * amplitude,
            Mathf.Sin(_shakeClock * 67.0f) * amplitude * 0.35f,
            Mathf.Cos(_shakeClock * 83.0f) * amplitude);
    }

    private void FindTarget()
    {
        _target = GetTree().GetFirstNodeInGroup("player_3d") as Node3D;
    }
}
