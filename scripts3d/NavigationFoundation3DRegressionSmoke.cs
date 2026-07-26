using Godot;

/// <summary>
/// Stage 3A regression smoke for baked planar navigation and Feral chasing.
/// It observes public navigation and attack contracts without owning combat
/// or pathfinding rules.
/// </summary>
public partial class NavigationFoundation3DRegressionSmoke : Node
{
    [Export] public PackedScene FeralScene { get; set; }

    private NavigationFoundationArena3D _arena;
    private PlayerController3D _player;
    private FeralController3D _feral;
    private GameFlowController3D _flow;
    private FeralController3D _dynamicFeral;
    private int _stage;
    private double _stageElapsed;
    private double _totalElapsed;
    private Vector3 _pausedPosition;
    private int _pausedRepathCount;
    private int _windupHealth;
    private int _windupImpactCount;
    private float _straightDistance;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _arena = GetNode<NavigationFoundationArena3D>("Arena3D");
        _arena.ProcessMode = ProcessModeEnum.Pausable;
        _player = _arena.GetNode<PlayerController3D>("Player3D");
        _feral = _arena.GetNode<FeralController3D>("Feral3D");
        _flow = _arena.GetNode<GameFlowController3D>("GameFlow3D");

        _player.GlobalPosition = new Vector3(8.5f, 0.0f, 0.0f);
        _feral.GlobalPosition = new Vector3(-8.5f, 0.0f, 0.0f);
        _feral.AttackCooldown = 0.4f;
        _straightDistance = _feral.GlobalPosition.DistanceTo(_player.GlobalPosition);
    }

    public override void _Process(double delta)
    {
        _totalElapsed += delta;
        _stageElapsed += delta;
        if (_totalElapsed > 35.0)
        {
            Fail($"timeout stage={_stage}");
            return;
        }

        switch (_stage)
        {
            case 0:
                ObserveBakedPath();
                break;
            case 1:
                PauseDuringChase();
                break;
            case 2:
                ObservePausedChase();
                break;
            case 3:
                ObserveFeralRouteAndWindup();
                break;
            case 4:
                ObserveFeralImpact();
                break;
            case 5:
                StartDynamicFeral();
                break;
            case 6:
                ObserveDynamicFeral();
                break;
            case 7:
                FinishSuccess();
                break;
        }
    }

    private void ObserveBakedPath()
    {
        var navigation = _feral.Navigation;
        if (navigation == null)
        {
            Fail("Feral has no EnemyNavigation3D adapter");
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
            $"baked path not ready ready={navigation.IsNavigationReady} path={navigation.HasPath} "
            + $"points={navigation.PathPointCount} turn={navigation.HasTurnPoint} reachable={navigation.IsTargetReachable} "
            + $"length={navigation.PathLength:0.00} straight={_straightDistance:0.00}");
    }

    private void PauseDuringChase()
    {
        if (_feral.State != FeralState3D.Chasing)
        {
            Fail("Feral entered attack flow before the pause navigation test");
            return;
        }

        _pausedPosition = _feral.GlobalPosition;
        _pausedRepathCount = _feral.Navigation.RepathCount;
        if (!_flow.RestoreState(GameFlowState.GameOver))
        {
            Fail("Could not enter GameOver for navigation pause test");
            return;
        }

        NextStage();
    }

    private void ObservePausedChase()
    {
        if (_stageElapsed < 0.8)
        {
            return;
        }

        if (_feral.GlobalPosition.DistanceTo(_pausedPosition) > 0.001f
            || _feral.Navigation.RepathCount != _pausedRepathCount)
        {
            Fail("GameOver pause advanced Feral position or RepathCount");
            return;
        }

        if (!_flow.RestoreState(GameFlowState.Playing))
        {
            Fail("Could not resume Playing after navigation pause test");
            return;
        }

        NextStage();
    }

    private void ObserveFeralRouteAndWindup()
    {
        if (IsInsideExpandedObstacle(_feral.GlobalPosition))
        {
            Fail($"Feral entered a navigation obstacle AABB pos={_feral.GlobalPosition} "
                + $"state={_feral.State} next={_feral.Navigation.DesiredDirection} "
                + $"path={_feral.Navigation.PathLength:0.00}");
            return;
        }

        if (_feral.State == FeralState3D.Windup)
        {
            _windupHealth = _player.CurrentHealth;
            _windupImpactCount = _feral.ImpactCount;
            if (_feral.ActiveTelegraph == null || !_feral.ActiveTelegraph.IsActive)
            {
                Fail("Feral Windup has no active telegraph");
                return;
            }

            NextStage();
            return;
        }

        if (_feral.Navigation.IsStuck)
        {
            Fail("Feral became permanently stuck while routing to the player");
            return;
        }

        FailIfTimedOut(
            $"Feral did not reach AttackRange state={_feral.State} "
            + $"distance={_feral.GlobalPosition.DistanceTo(_player.GlobalPosition):0.00}");
    }

    private void ObserveFeralImpact()
    {
        if (_feral.State == FeralState3D.Windup)
        {
            if (_player.CurrentHealth != _windupHealth
                || _feral.ImpactCount != _windupImpactCount)
            {
                Fail("Feral caused damage before Impact");
                return;
            }

            FailIfTimedOut("Feral Windup did not reach Impact");
            return;
        }

        if (_feral.ImpactCount != _windupImpactCount + 1)
        {
            FailIfTimedOut("Feral did not produce one Impact");
            return;
        }

        if (_player.CurrentHealth >= _windupHealth
            || _feral.SuccessfulContactAttackCount < 1
            || _feral.ActiveTelegraph != null)
        {
            Fail("Feral Impact contract was not preserved");
            return;
        }

        _feral.SetPhysicsProcess(false);
        NextStage();
    }

    private void StartDynamicFeral()
    {
        if (FeralScene == null)
        {
            Fail("Navigation smoke has no FeralScene");
            return;
        }

        _dynamicFeral = FeralScene.Instantiate<FeralController3D>();
        _arena.AddChild(_dynamicFeral);
        _dynamicFeral.GlobalPosition = new Vector3(-8.5f, 0.0f, 0.0f);
        NextStage();
    }

    private void ObserveDynamicFeral()
    {
        if (_dynamicFeral == null || !GodotObject.IsInstanceValid(_dynamicFeral))
        {
            Fail("Dynamic Feral was removed before reaching the target");
            return;
        }

        if (IsInsideExpandedObstacle(_dynamicFeral.GlobalPosition))
        {
            Fail($"Dynamic Feral entered a navigation obstacle AABB pos={_dynamicFeral.GlobalPosition}");
            return;
        }

        var navigation = _dynamicFeral.Navigation;
        if (navigation == null || !navigation.IsNavigationReady)
        {
            FailIfTimedOut("Dynamic Feral did not register with the navigation map");
            return;
        }

        if (_dynamicFeral.State == FeralState3D.Windup
            || _dynamicFeral.GlobalPosition.DistanceTo(_player.GlobalPosition)
                <= _dynamicFeral.AttackRange + 0.15f)
        {
            _dynamicFeral.SetPhysicsProcess(false);
            _dynamicFeral.QueueFree();
            NextStage();
            return;
        }

        if (navigation.IsStuck)
        {
            Fail("Dynamic Feral became stuck while routing");
            return;
        }

        FailIfTimedOut("Dynamic Feral did not route around the obstacle");
    }

    private void FinishSuccess()
    {
        GD.Print("NavigationFoundation3DRegressionSmoke PASS");
        GetTree().Quit(0);
    }

    private bool IsInsideExpandedObstacle(Vector3 position)
    {
        const float margin = 0.0f;
        return IsInside(_arena.ObstacleA, position, margin)
            || IsInside(_arena.ObstacleB, position, margin);
    }

    private static bool IsInside(Aabb obstacle, Vector3 position, float margin)
    {
        var min = obstacle.Position - new Vector3(margin, 0.0f, margin);
        var max = obstacle.End + new Vector3(margin, 0.0f, margin);
        return position.X >= min.X && position.X <= max.X
            && position.Z >= min.Z && position.Z <= max.Z;
    }

    private void NextStage()
    {
        _stage++;
        _stageElapsed = 0.0;
    }

    private void FailIfTimedOut(string message)
    {
        if (_stageElapsed > 8.0)
        {
            Fail(message);
        }
    }

    private void Fail(string message)
    {
        GD.PushError($"NavigationFoundation3DRegressionSmoke FAIL: {message}");
        GetTree().Quit(1);
    }
}
