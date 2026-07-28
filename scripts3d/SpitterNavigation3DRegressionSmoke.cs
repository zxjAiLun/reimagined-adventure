using Godot;

/// <summary>
/// Stage 3B smoke for Spitter distance-band movement, obstacle repositioning
/// and compatibility with the locked Aim/Windup attack contract.
/// </summary>
public partial class SpitterNavigation3DRegressionSmoke : Node
{
    [Export] public PackedScene SpitterScene { get; set; }

    private NavigationFoundationArena3D _arena;
    private PlayerController3D _player;
    private FeralController3D _feral;
    private SpitterController3D _spitter;
    private int _stage;
    private double _stageElapsed;
    private double _totalElapsed;
    private float _straightDistance;
    private bool _retreatObserved;
    private Vector3 _windupDirection;
    private Vector3 _windupTelegraphStart;
    private Vector3 _windupTelegraphEnd;
    private Vector3 _windupPosition;
    private int _windupShotCount;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _arena = GetNode<NavigationFoundationArena3D>("Arena3D");
        _arena.ProcessMode = ProcessModeEnum.Pausable;
        _player = _arena.GetNode<PlayerController3D>("Player3D");
        _feral = _arena.GetNode<FeralController3D>("Feral3D");

        _feral.SetPhysicsProcess(false);
        _feral.CollisionLayer = 0;
        _feral.CollisionMask = 0;

        _player.GlobalPosition = new Vector3(8.5f, 0.0f, 0.0f);
        if (SpitterScene == null)
        {
            return;
        }

        _spitter = SpitterScene.Instantiate<SpitterController3D>();
        _spitter.PreferredRange = 5.5f;
        _spitter.MinimumRange = 3.0f;
        _spitter.MoveSpeed = 2.4f;
        _spitter.AimSeconds = 0.18f;
        _spitter.TelegraphSeconds = 0.42f;
        _spitter.RecoverySeconds = 0.25f;
        _arena.AddChild(_spitter);
        _spitter.GlobalPosition = new Vector3(-8.5f, 0.0f, 0.0f);
        _straightDistance = _spitter.GlobalPosition.DistanceTo(_player.GlobalPosition);
    }

    public override void _Process(double delta)
    {
        _totalElapsed += delta;
        _stageElapsed += delta;
        if (_totalElapsed > 30.0)
        {
            Fail($"timeout stage={_stage}");
            return;
        }

        if (_spitter == null)
        {
            Fail("SpitterScene was not configured");
            return;
        }

        switch (_stage)
        {
            case 0:
                ObserveApproachPath();
                break;
            case 1:
                ObserveDistanceBand();
                break;
            case 2:
                StartRetreat();
                break;
            case 3:
                ObserveRetreat();
                break;
            case 4:
                ObserveLockedWindupStart();
                break;
            case 5:
                ObserveLockedWindup();
                break;
            case 6:
                FinishSuccess();
                break;
        }
    }

    private void ObserveApproachPath()
    {
        var navigation = _spitter.Navigation;
        if (navigation == null)
        {
            Fail("Spitter has no EnemyNavigation3D adapter");
            return;
        }

        if (IsInsideObstacle(_spitter.GlobalPosition))
        {
            Fail($"Spitter entered a navigation obstacle during approach pos={_spitter.GlobalPosition}");
            return;
        }

        if (navigation.IsNavigationReady
            && navigation.HasPath
            && navigation.PathPointCount >= 3
            && navigation.HasTurnPoint
            && navigation.IsTargetReachable
            && navigation.PathLength > _straightDistance + 1.0f)
        {
            NextStage();
            return;
        }

        FailIfTimedOut(
            $"Spitter approach path not ready ready={navigation.IsNavigationReady} "
            + $"path={navigation.HasPath} points={navigation.PathPointCount} turn={navigation.HasTurnPoint} "
            + $"reachable={navigation.IsTargetReachable} length={navigation.PathLength:0.00}");
    }

    private void ObserveDistanceBand()
    {
        if (IsInsideObstacle(_spitter.GlobalPosition))
        {
            Fail($"Spitter entered a navigation obstacle before holding range pos={_spitter.GlobalPosition}");
            return;
        }

        var distance = HorizontalDistance(_spitter.GlobalPosition, _player.GlobalPosition);
        if (distance >= _spitter.MinimumRange - 0.15f
            && distance <= _spitter.PreferredRange + 0.15f
            && _spitter.State != SpitterState3D.Approaching
            && _spitter.State != SpitterState3D.Retreating)
        {
            NextStage();
            return;
        }

        if (_spitter.Navigation.IsStuck)
        {
            Fail("Spitter became stuck while approaching its distance band");
            return;
        }

        FailIfTimedOut(
            $"Spitter did not enter distance band state={_spitter.State} distance={distance:0.00} "
            + $"pos={_spitter.GlobalPosition} ready={_spitter.Navigation.IsNavigationReady} "
            + $"path={_spitter.Navigation.HasPath} direction={_spitter.Navigation.DesiredDirection} "
            + $"target={_spitter.NavigationTargetPosition} "
            + $"finished={_spitter.Navigation.Agent.IsNavigationFinished()} "
            + $"next={_spitter.Navigation.Agent.GetNextPathPosition()}",
            14.0);
    }

    private void StartRetreat()
    {
        _player.GlobalPosition = _spitter.GlobalPosition + Vector3.Right * 0.75f;
        _retreatObserved = false;
        NextStage();
    }

    private void ObserveRetreat()
    {
        if (IsInsideObstacle(_spitter.GlobalPosition))
        {
            Fail($"Spitter entered a navigation obstacle while retreating pos={_spitter.GlobalPosition}");
            return;
        }

        if (_spitter.State == SpitterState3D.Retreating)
        {
            _retreatObserved = true;
            var toPlayer = _player.GlobalPosition - _spitter.GlobalPosition;
            toPlayer.Y = 0.0f;
            var away = toPlayer.LengthSquared() > 0.001f
                ? -toPlayer.Normalized()
                : Vector3.Back;
            var targetDelta = _spitter.NavigationTargetPosition - _spitter.GlobalPosition;
            targetDelta.Y = 0.0f;
            if (_spitter.Navigation == null
                || !_spitter.Navigation.IsNavigationReady
                || targetDelta.LengthSquared() <= 0.001f
                || targetDelta.Normalized().Dot(away) < 0.75f)
            {
                Fail("Spitter retreat did not submit a navigation target away from the player");
                return;
            }
        }

        var distance = HorizontalDistance(_spitter.GlobalPosition, _player.GlobalPosition);
        if (_retreatObserved
            && distance >= _spitter.MinimumRange - 0.15f
            && _spitter.State != SpitterState3D.Retreating)
        {
            NextStage();
            return;
        }

        if (_spitter.Navigation.IsStuck)
        {
            Fail("Spitter became stuck while retreating to its distance band");
            return;
        }

        FailIfTimedOut(
            $"Spitter did not recover its distance band retreat={_retreatObserved} "
            + $"state={_spitter.State} distance={distance:0.00}");
    }

    private void ObserveLockedWindupStart()
    {
        if (_spitter.State != SpitterState3D.Windup)
        {
            FailIfTimedOut($"Spitter did not enter Windup after repositioning state={_spitter.State}");
            return;
        }

        if (_spitter.ActiveTelegraph == null || !_spitter.ActiveTelegraph.IsActive)
        {
            Fail("Spitter Windup has no active line telegraph");
            return;
        }

        _windupDirection = _spitter.ActiveTelegraph.LockedDirection;
        _windupTelegraphStart = _spitter.ActiveTelegraph.StartPosition;
        _windupTelegraphEnd = _spitter.ActiveTelegraph.EndPosition;
        _windupPosition = _spitter.GlobalPosition;
        _windupShotCount = _spitter.ProjectileShotCount;
        _player.GlobalPosition += new Vector3(2.0f, 0.0f, -1.5f);
        NextStage();
    }

    private void ObserveLockedWindup()
    {
        if (_spitter.State == SpitterState3D.Windup)
        {
            var telegraph = _spitter.ActiveTelegraph;
            if (telegraph == null
                || !telegraph.IsActive
                || telegraph.LockedDirection.DistanceTo(_windupDirection) > 0.001f
                || telegraph.StartPosition.DistanceTo(_windupTelegraphStart) > 0.001f
                || telegraph.EndPosition.DistanceTo(_windupTelegraphEnd) > 0.001f
                || _spitter.GlobalPosition.DistanceTo(_windupPosition) > 0.001f
                || _spitter.ProjectileShotCount != _windupShotCount)
            {
                Fail("Spitter Windup lost its locked position, direction, telegraph or shot gate");
                return;
            }

            return;
        }

        if (_spitter.ProjectileShotCount == _windupShotCount + 1)
        {
            if (_spitter.LastLaunchDirection.DistanceTo(_windupDirection) > 0.001f
                || _spitter.ActiveTelegraph != null)
            {
                Fail("Spitter Launch did not preserve the locked direction");
                return;
            }

            NextStage();
            return;
        }

        FailIfTimedOut($"Spitter did not launch after locked Windup state={_spitter.State}");
    }

    private void FinishSuccess()
    {
        GD.Print("SPITTER_NAVIGATION_3D_PASS approach=true range=true retreat=true windup=true lock=true");
        GetTree().Quit(0);
    }

    private bool IsInsideObstacle(Vector3 position)
    {
        return IsInside(_arena.ObstacleA, position, 0.0f)
            || IsInside(_arena.ObstacleB, position, 0.0f);
    }

    private static bool IsInside(Aabb obstacle, Vector3 position, float margin)
    {
        var min = obstacle.Position - new Vector3(margin, 0.0f, margin);
        var max = obstacle.End + new Vector3(margin, 0.0f, margin);
        return position.X >= min.X && position.X <= max.X
            && position.Z >= min.Z && position.Z <= max.Z;
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.Y = 0.0f;
        second.Y = 0.0f;
        return first.DistanceTo(second);
    }

    private void NextStage()
    {
        _stage++;
        _stageElapsed = 0.0;
    }

    private void FailIfTimedOut(string message, double timeoutSeconds = 8.0)
    {
        if (_stageElapsed > timeoutSeconds)
        {
            Fail(message);
        }
    }

    private void Fail(string message)
    {
        GD.PushError($"SpitterNavigation3DRegressionSmoke FAIL: {message}");
        GetTree().Quit(1);
    }
}
