using Godot;

/// <summary>
/// Small navigation adapter for planar enemy movement. It owns only target
/// refresh, NavigationAgent3D path sampling and stuck detection; actor combat
/// state and movement velocity remain in the owning actor.
/// </summary>
public partial class EnemyNavigation3D : Node
{
    [Export] public float TargetMoveThreshold { get; set; } = 0.35f;
    [Export] public float RepathIntervalSeconds { get; set; } = 0.25f;
    [Export] public float StuckDurationSeconds { get; set; } = 1.25f;
    [Export] public float MinimumProgressDistance { get; set; } = 0.12f;

    public NavigationAgent3D Agent => _agent;
    public Vector3 TargetPosition { get; private set; }
    public Vector3 DesiredDirection { get; private set; }
    public bool IsNavigationReady { get; private set; }
    public bool HasPath { get; private set; }
    public bool IsTargetReachable { get; private set; }
    public bool IsStuck { get; private set; }
    public bool HasTurnPoint { get; private set; }
    public int RepathCount { get; private set; }
    public int StuckDetectionCount { get; private set; }
    public int PathPointCount { get; private set; }
    public float PathLength { get; private set; }

    private NavigationAgent3D _agent;
    private bool _hasTarget;
    private float _targetRefreshRemaining;
    private float _stuckElapsed;
    private float _progressDistance;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        _agent = GetNodeOrNull<NavigationAgent3D>("../NavigationAgent3D")
            ?? GetNodeOrNull<NavigationAgent3D>("NavigationAgent3D");
        if (_agent == null)
        {
            GD.PushError("EnemyNavigation3D requires a NavigationAgent3D child.");
            return;
        }

        _agent.PathDesiredDistance = 0.35f;
        _agent.TargetDesiredDistance = 1.1f;
        _agent.PathMaxDistance = 5.0f;
        _agent.AvoidanceEnabled = false;
    }

    public void SetTarget(Vector3 targetPosition)
    {
        targetPosition.Y = 0.0f;
        var targetMoved = !_hasTarget || HorizontalDistance(TargetPosition, targetPosition)
            > Mathf.Max(0.01f, TargetMoveThreshold);
        var timedRepath = _targetRefreshRemaining <= 0.0f;
        var stuckRepath = IsStuck;
        if (!_hasTarget || targetMoved || timedRepath || stuckRepath)
        {
            TargetPosition = targetPosition;
            _hasTarget = true;
            _targetRefreshRemaining = Mathf.Max(0.05f, RepathIntervalSeconds);
            RepathCount++;
            if (_agent != null)
            {
                _agent.TargetPosition = TargetPosition;
            }

            if (targetMoved)
            {
                ResetProgressTracking();
            }

            if (stuckRepath)
            {
                IsStuck = false;
                ResetProgressTracking();
            }
        }
    }

    public Vector3 GetDesiredDirection(Vector3 currentPosition, float delta)
    {
        _targetRefreshRemaining = Mathf.Max(0.0f, _targetRefreshRemaining - delta);
        DesiredDirection = Vector3.Zero;
        HasPath = false;
        IsTargetReachable = false;
        PathPointCount = 0;
        PathLength = 0.0f;
        HasTurnPoint = false;

        IsNavigationReady = _agent != null && HasSynchronizedNavigationMap();
        if (!IsNavigationReady || !_hasTarget)
        {
            return DesiredDirection;
        }

        // NavigationAgent3D requires this to be called from the physics loop.
        var nextPathPosition = _agent.GetNextPathPosition();
        var path = _agent.GetCurrentNavigationPath();
        PathPointCount = path.Length;
        HasPath = PathPointCount > 0;
        for (var index = 1; index < path.Length; index++)
        {
            PathLength += path[index - 1].DistanceTo(path[index]);
        }

        for (var index = 2; index < path.Length; index++)
        {
            var incoming = path[index - 1] - path[index - 2];
            var outgoing = path[index] - path[index - 1];
            incoming.Y = 0.0f;
            outgoing.Y = 0.0f;
            if (incoming.LengthSquared() > 0.001f
                && outgoing.LengthSquared() > 0.001f
                && incoming.Normalized().Dot(outgoing.Normalized()) < 0.995f)
            {
                HasTurnPoint = true;
                break;
            }
        }

        IsTargetReachable = HasPath && _agent.IsTargetReachable();
        var direction = nextPathPosition - currentPosition;
        direction.Y = 0.0f;
        if (direction.LengthSquared() > 0.001f && !_agent.IsNavigationFinished())
        {
            DesiredDirection = direction.Normalized();
        }

        return DesiredDirection;
    }

    public void NotifyMovement(Vector3 previousPosition, Vector3 currentPosition, float delta)
    {
        if (!IsNavigationReady || DesiredDirection.LengthSquared() <= 0.001f)
        {
            _stuckElapsed = 0.0f;
            _progressDistance = 0.0f;
            return;
        }

        previousPosition.Y = 0.0f;
        currentPosition.Y = 0.0f;
        _progressDistance += previousPosition.DistanceTo(currentPosition);
        _stuckElapsed += Mathf.Max(0.0f, delta);
        if (_progressDistance >= Mathf.Max(0.01f, MinimumProgressDistance))
        {
            _stuckElapsed = 0.0f;
            _progressDistance = 0.0f;
            IsStuck = false;
        }
        else if (_stuckElapsed >= Mathf.Max(0.1f, StuckDurationSeconds))
        {
            if (!IsStuck)
            {
                IsStuck = true;
                StuckDetectionCount++;
            }

            _targetRefreshRemaining = 0.0f;
        }
    }

    public void Stop()
    {
        DesiredDirection = Vector3.Zero;
        HasPath = false;
        IsTargetReachable = false;
    }

    private bool HasSynchronizedNavigationMap()
    {
        var map = _agent.GetNavigationMap();
        return map.IsValid && NavigationServer3D.MapGetIterationId(map) > 0;
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.Y = 0.0f;
        second.Y = 0.0f;
        return first.DistanceTo(second);
    }

    private void ResetProgressTracking()
    {
        _stuckElapsed = 0.0f;
        _progressDistance = 0.0f;
    }
}
