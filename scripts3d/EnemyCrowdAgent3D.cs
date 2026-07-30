using Godot;

/// <summary>
/// Adapter attached to one enemy. It receives pair results from the
/// coordinator and combines them with navigation only while the actor is in
/// an explicitly movable state.
/// </summary>
public partial class EnemyCrowdAgent3D : Node
{
    [Export] public float BodyRadius { get; set; } = 0.55f;
    [Export] public float PersonalSpace { get; set; } = 0.10f;
    [Export] public float SeparationWeight { get; set; } = 0.85f;
    [Export] public float MaxSeparationSpeedRatio { get; set; } = 0.60f;
    [Export] public float CongestionSeconds { get; set; } = 0.80f;
    [Export] public float MinimumProgressDistance { get; set; } = 0.08f;
    [Export] public float CongestionPressureThreshold { get; set; } = 0.25f;

    private EnemyCrowdCoordinator3D _coordinator;
    private Vector3 _pairCorrection;
    private float _pairPressure;
    private int _neighborCount;
    private float _congestionElapsed;
    private float _progressDistance;
    private float _recoveryDelayRemaining;
    private float _recoveryRemaining;
    private bool _recoveryPending;
    private EnemyNavigation3D _pendingRecoveryNavigation;
    private bool _active = true;

    public int CrowdId { get; private set; }
    public bool IsActive => _active;
    public Vector3 WorldPosition => GetParent<Node3D>()?.GlobalPosition ?? Vector3.Zero;
    public int NeighborCount => _neighborCount;
    public float CrowdPressure => _pairPressure;
    public Vector3 SeparationVelocity => _pairCorrection;
    public bool IsCongested { get; private set; }
    public bool IsRecovering => _recoveryRemaining > 0.0f;
    public int CongestionRecoveryCount { get; private set; }
    public long LastRecoveryPhysicsFrame { get; private set; } = -1;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        TryRegisterWithCoordinator();
    }

    public override void _ExitTree()
    {
        _coordinator?.Unregister(this);
        _coordinator = null;
    }

    public void SetActive(bool active)
    {
        if (_active == active)
        {
            return;
        }

        _active = active;
        if (!active)
        {
            _coordinator?.Unregister(this);
            _pairCorrection = Vector3.Zero;
            _pairPressure = 0.0f;
            _neighborCount = 0;
            IsCongested = false;
        }
        else
        {
            TryRegisterWithCoordinator();
        }
    }

    public Vector3 CombineNavigationVelocity(Vector3 navigationVelocity, float moveSpeed)
    {
        if (!_active || navigationVelocity.LengthSquared() <= 0.000001f || moveSpeed <= 0.0f)
        {
            return Vector3.Zero;
        }

        var forward = navigationVelocity.Normalized();
        var weight = SeparationWeight * (IsRecovering ? 0.35f : 1.0f);
        var correction = LimitLength(_pairCorrection * weight, moveSpeed * MaxSeparationSpeedRatio);

        // Separation may not completely reverse the navigation direction.
        var minimumForwardDot = -moveSpeed * 0.35f;
        var correctionDot = correction.Dot(forward);
        if (correctionDot < minimumForwardDot)
        {
            correction -= forward * (correctionDot - minimumForwardDot);
        }

        var combined = LimitLength(navigationVelocity + correction, moveSpeed);
        if (combined.Dot(forward) < 0.0f)
        {
            return forward * Mathf.Min(moveSpeed, navigationVelocity.Length());
        }

        return combined;
    }

    public void NotifyMovement(
        EnemyNavigation3D navigation,
        Vector3 navigationDirection,
        Vector3 previousPosition,
        Vector3 currentPosition,
        float delta)
    {
        if (!_active || delta <= 0.0f)
        {
            return;
        }

        if (_recoveryRemaining > 0.0f)
        {
            _recoveryRemaining = Mathf.Max(0.0f, _recoveryRemaining - delta);
        }

        var planarNavigation = navigationDirection;
        planarNavigation.Y = 0.0f;
        if (planarNavigation.LengthSquared() <= 0.000001f
            || _neighborCount < 2
            || _pairPressure < CongestionPressureThreshold)
        {
            _congestionElapsed = 0.0f;
            _progressDistance = 0.0f;
            IsCongested = false;
            return;
        }

        var actualMovement = currentPosition - previousPosition;
        actualMovement.Y = 0.0f;
        _progressDistance += actualMovement.Length();

        if (_progressDistance >= MinimumProgressDistance)
        {
            _congestionElapsed = 0.0f;
            _progressDistance = 0.0f;
            IsCongested = false;
            return;
        }

        _congestionElapsed += delta;
        IsCongested = _congestionElapsed >= CongestionSeconds;

        if (!_recoveryPending && IsCongested)
        {
            _recoveryPending = true;
            _pendingRecoveryNavigation = navigation;
            _recoveryDelayRemaining = (CrowdId % 4) * 0.05f;
            if (_recoveryDelayRemaining <= 0.0f)
            {
                BeginRecovery();
            }
        }
        else if (_recoveryPending)
        {
            _recoveryDelayRemaining -= delta;
            if (_recoveryDelayRemaining <= 0.0f)
            {
                BeginRecovery();
            }
        }
    }

    internal void AssignCoordinator(EnemyCrowdCoordinator3D coordinator, int crowdId)
    {
        _coordinator = coordinator;
        CrowdId = crowdId;
    }

    internal void ClearCoordinator(EnemyCrowdCoordinator3D coordinator)
    {
        if (ReferenceEquals(_coordinator, coordinator))
        {
            _coordinator = null;
        }
    }

    internal void BeginCrowdFrame()
    {
        _pairCorrection = Vector3.Zero;
        _pairPressure = 0.0f;
        _neighborCount = 0;
    }

    internal void ReceivePair(Vector3 correction, float pressure)
    {
        _pairCorrection += correction * 4.0f;
        _pairPressure += pressure;
        _neighborCount++;
    }

    internal void FinishCrowdFrame()
    {
        _pairCorrection.Y = 0.0f;
    }

    private void TryRegisterWithCoordinator()
    {
        if (!_active || _coordinator != null || !IsInsideTree())
        {
            return;
        }

        var coordinator = GetTree().GetFirstNodeInGroup("enemy_crowd_coordinators_3d")
            as EnemyCrowdCoordinator3D;
        coordinator?.Register(this);
    }

    private void BeginRecovery()
    {
        _recoveryPending = false;
        _recoveryDelayRemaining = 0.0f;
        _recoveryRemaining = 0.35f;
        _congestionElapsed = 0.0f;
        _progressDistance = 0.0f;
        CongestionRecoveryCount++;
        LastRecoveryPhysicsFrame = (long)Engine.GetPhysicsFrames();
        _pendingRecoveryNavigation?.RequestRepath();
        _pendingRecoveryNavigation = null;
    }

    private static Vector3 LimitLength(Vector3 value, float maximum)
    {
        if (maximum <= 0.0f)
        {
            return Vector3.Zero;
        }

        var length = value.Length();
        return length > maximum ? value / length * maximum : value;
    }
}
