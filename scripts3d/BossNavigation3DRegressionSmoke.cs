using System;
using System.Collections.Generic;
using Arpg.Domain;
using Godot;

/// <summary>
/// Stage 3D contract for large-agent navigation, locked Boss attacks, pause
/// safety and immediate crowd/physics/loot cleanup on death.
/// </summary>
public partial class BossNavigation3DRegressionSmoke : Node
{
    private BossNavigationStressArena3D _arena;
    private EnemyCrowdCoordinator3D _coordinator;
    private PlayerController3D _player;
    private GameFlowController3D _flow;
    private BrimstoneColossusController3D _boss;
    private FeralController3D _pathFeral;
    private readonly List<FeralController3D> _pressureFerals = new();

    private int _stage;
    private double _stageElapsed;
    private double _totalElapsed;
    private int _initialSlamImpactCount;
    private int _initialSpearLaunchCount;
    private int _pauseUpdateCount;
    private int _pauseRepathCount;
    private int _pauseStuckCount;
    private Vector3 _slamPosition;
    private Vector3 _slamCenter;
    private Vector3 _slamTelegraphPosition;
    private float _slamTelegraphRadius;
    private Vector3 _spearPosition;
    private Vector3 _spearStart;
    private Vector3 _spearEnd;
    private Vector3 _spearDirection;
    private Vector3 _pausePosition;
    private bool _navContractObserved;
    private bool _slamPressurePlaced;
    private bool _spearPressurePlaced;
    private bool _slamSnapshotCaptured;
    private bool _spearSnapshotCaptured;
    private bool _pauseStarted;
    private bool _noPathLayerDisabled;
    private bool _noPathObserved;
    private Vector3 _noPathGuardPosition;
    private int _noPathSlamCount;
    private int _noPathSpearCount;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _arena = GetNode<BossNavigationStressArena3D>("Arena3D");
        _arena.ProcessMode = ProcessModeEnum.Pausable;
        _coordinator = _arena.Coordinator;
        _player = _arena.Player;
        _flow = _arena.GetNode<GameFlowController3D>("GameFlow3D");
        _player.GetNode<HealthComponent>("HealthComponent").SetMaxHealth(100000);
    }

    public override void _Process(double delta)
    {
        _totalElapsed += delta;
        _stageElapsed += delta;
        if (_totalElapsed > 60.0)
        {
            Fail($"timeout stage={_stage}");
            return;
        }

        switch (_stage)
        {
            case 0:
                WaitForArena();
                break;
            case 1:
                WaitForPressureAgents();
                break;
            case 2:
                ObserveNavigationLayers();
                break;
            case 3:
                ObserveNoPathGuard();
                break;
            case 4:
                ObserveBossRoute();
                break;
            case 5:
                ObserveSlamLock();
                break;
            case 6:
                PrepareSpearLock();
                break;
            case 7:
                ObserveSpearLock();
                break;
            case 8:
                PreparePause();
                break;
            case 9:
                ObservePause();
                break;
            case 10:
                ObserveResumeThenDeath();
                break;
        }
    }

    private void WaitForArena()
    {
        _boss = _arena.Boss;
        if (_boss == null || _coordinator == null || _coordinator.RegisteredAgentCount != 2)
        {
            return;
        }

        _pathFeral = FindFeral();
        if (_pathFeral == null)
        {
            Fail("initial Boss navigation arena did not register a Feral");
            return;
        }

        _arena.SpawnAdditionalWave(2, 0);
        NextStage();
    }

    private void WaitForPressureAgents()
    {
        if (_coordinator.RegisteredAgentCount != 4)
        {
            FailIfTimedOut($"pressure agent registration={_coordinator.RegisteredAgentCount}/4", 4.0);
            return;
        }

        _pressureFerals.Clear();
        for (var i = 0; i < _coordinator.RegisteredAgentCount; i++)
        {
            var feral = _coordinator.GetRegisteredAgent(i)?.GetParent<Node3D>() as FeralController3D;
            if (feral != null)
            {
                _pressureFerals.Add(feral);
            }
        }

        if (_pressureFerals.Count != 3)
        {
            Fail($"expected three Feral crowd neighbors, found {_pressureFerals.Count}");
            return;
        }

        // Keep the future pressure neighbors out of the route until the Boss
        // reaches the attack area. Their Agents remain registered and active.
        for (var i = 0; i < _pressureFerals.Count; i++)
        {
            if (_pressureFerals[i] != _pathFeral)
            {
                _pressureFerals[i].SetPhysicsProcess(false);
                _pressureFerals[i].GlobalPosition = new Vector3(-10.0f, 0.0f, -8.5f + i);
            }
        }

        NextStage();
    }

    private void ObserveNavigationLayers()
    {
        var bossNavigation = _boss.Navigation;
        var feralNavigation = _pathFeral.Navigation;
        if (bossNavigation == null || feralNavigation == null
            || !bossNavigation.IsNavigationReady || !feralNavigation.IsNavigationReady
            || !bossNavigation.HasPath || !feralNavigation.HasPath)
        {
            FailIfTimedOut(
                "large/small navigation did not become ready"
                + $" bossReady={bossNavigation?.IsNavigationReady} bossPath={bossNavigation?.HasPath}"
                + $" feralReady={feralNavigation?.IsNavigationReady} feralPath={feralNavigation?.HasPath}"
                + $" bossPos={_boss.GlobalPosition} bossLayers={bossNavigation?.Agent?.NavigationLayers}"
                + $" bossState={_boss.State} bossPhysics={_boss.IsPhysicsProcessing()}"
                + $" bossAlive={_boss.IsAlive}"
                + $" bossRepath={bossNavigation?.RepathCount} bossPoints={bossNavigation?.PathPointCount}"
                + $" bossMap={FormatNavigationMap(bossNavigation?.Agent)}",
                15.0);
            return;
        }

        if (bossNavigation.Agent.NavigationLayers != 2
            || feralNavigation.Agent.NavigationLayers != 1)
        {
            Fail($"navigation layers mismatch boss={bossNavigation.Agent.NavigationLayers} "
                + $"feral={feralNavigation.Agent.NavigationLayers}");
            return;
        }

        if (bossNavigation.PathPointCount < 3
            || bossNavigation.PathLength <= HorizontalDistance(_boss.GlobalPosition, _player.GlobalPosition)
            || PathContainsAabb(bossNavigation.Agent.GetCurrentNavigationPath(), _arena.NarrowChoke))
        {
            Fail("large Boss path did not prove a turning route outside the narrow choke"
                + $" points={bossNavigation.PathPointCount} length={bossNavigation.PathLength:F2}"
                + $" direct={HorizontalDistance(_boss.GlobalPosition, _player.GlobalPosition):F2}"
                + $" path={FormatPath(bossNavigation.Agent.GetCurrentNavigationPath())}");
            return;
        }

        if (feralNavigation.PathPointCount < 3
            || !PathContainsAabb(feralNavigation.Agent.GetCurrentNavigationPath(), _arena.NarrowChoke, 0.7f))
        {
            Fail("small-agent Feral path did not prove access to the narrow route"
                + $" points={feralNavigation.PathPointCount} path={FormatPath(feralNavigation.Agent.GetCurrentNavigationPath())}");
            return;
        }

        _navContractObserved = true;
        NextStage();
    }

    private void ObserveNoPathGuard()
    {
        var navigation = _boss.Navigation;
        if (navigation == null || navigation.Agent == null)
        {
            Fail("Boss navigation adapter disappeared before no-path guard");
            return;
        }

        if (!_noPathLayerDisabled)
        {
            _boss.GlobalPosition = new Vector3(-10.0f, 0.0f, 0.0f);
            _player.GlobalPosition = new Vector3(10.0f, 0.0f, 0.0f);
            navigation.Agent.NavigationLayers = 4;
            navigation.RequestRepath();
            _noPathGuardPosition = _boss.GlobalPosition;
            _noPathSlamCount = _boss.MagmaSlamCount;
            _noPathSpearCount = _boss.FlameSpearCount;
            _noPathLayerDisabled = true;
            _stageElapsed = 0.0;
            return;
        }

        var noPath = !navigation.IsNavigationReady
            || !navigation.HasPath
            || !navigation.IsTargetReachable;
        _noPathObserved |= noPath;
        if (_stageElapsed < 0.8)
        {
            if (navigation.IsNavigationReady
                && navigation.HasPath
                && navigation.IsTargetReachable)
            {
                FailIfTimedOut(
                    "Boss navigation layer override did not produce an unusable path",
                    3.0);
            }

            return;
        }

        if (!_noPathObserved)
        {
            Fail("Boss no-path guard never observed an unavailable navigation route");
            return;
        }

        if (_boss.GlobalPosition.DistanceTo(_noPathGuardPosition) > 0.01f
            || _boss.MagmaSlamCount != _noPathSlamCount
            || _boss.FlameSpearCount != _noPathSpearCount
            || IsInsideObstacle(_boss.GlobalPosition)
            || IsInside(_arena.NarrowChoke, _boss.GlobalPosition, 0.0f))
        {
            Fail("Boss moved or attacked while navigation was unavailable");
            return;
        }

        navigation.Agent.NavigationLayers = 2;
        navigation.RequestRepath();
        NextStage();
    }

    private void ObserveBossRoute()
    {
        if (!_navContractObserved)
        {
            Fail("Boss route stage started without navigation contract");
            return;
        }

        if (IsInsideObstacle(_boss.GlobalPosition))
        {
            Fail($"Boss entered static obstacle pos={_boss.GlobalPosition}");
            return;
        }

        if (IsInside(_arena.NarrowChoke, _boss.GlobalPosition, 0.0f))
        {
            Fail($"Boss entered small-agent narrow choke pos={_boss.GlobalPosition}");
            return;
        }

        if (_boss.Navigation.IsStuck)
        {
            Fail("Boss remained stuck during large-agent route");
            return;
        }

        var distance = HorizontalDistance(_boss.GlobalPosition, _player.GlobalPosition);
        if (!_slamPressurePlaced && distance < 5.0f)
        {
            PlacePressureNeighbors(_boss.GlobalPosition);
            _initialSlamImpactCount = _boss.MagmaSlamImpactCount;
            _slamPressurePlaced = true;
            NextStage();
            return;
        }

        FailIfTimedOut(
            $"Boss did not navigate into Slam range distance={distance:0.00} "
            + $"pos={_boss.GlobalPosition} direction={_boss.Navigation.DesiredDirection} "
            + $"reachable={_boss.Navigation.IsTargetReachable} path={_boss.Navigation.PathLength:0.00} "
            + $"agentIndex={_boss.Navigation.Agent.GetCurrentNavigationPathIndex()} "
            + $"steeringIndex={_boss.Navigation.SteeringPathIndex} "
            + $"steeringTarget={_boss.Navigation.SteeringTargetPosition} "
            + $"lookahead={_boss.Navigation.UsePlanarPathLookahead} "
            + $"points={FormatPath(_boss.Navigation.Agent.GetCurrentNavigationPath())}", 30.0);
    }

    private void ObserveSlamLock()
    {
        if (_boss.State == BrimstoneColossusState3D.PreparingSlam
            && _boss.ActiveSlamTelegraph != null
            && _boss.CrowdAgent != null
            && _boss.CrowdAgent.NeighborCount >= 3)
        {
            if (!_slamSnapshotCaptured)
            {
                _slamPosition = _boss.GlobalPosition;
                _slamCenter = _boss.LockedSlamCenter;
                _slamTelegraphPosition = _boss.ActiveSlamTelegraph.GlobalPosition;
                _slamTelegraphRadius = _boss.ActiveSlamTelegraph.Radius;
                _slamSnapshotCaptured = true;
                _player.GlobalPosition += new Vector3(0.0f, 0.0f, 2.0f);
            }

            if (_boss.GlobalPosition.DistanceTo(_slamPosition) > 0.01f
                || _boss.LockedSlamCenter.DistanceTo(_slamCenter) > 0.01f
                || _boss.ActiveSlamTelegraph.GlobalPosition.DistanceTo(_slamTelegraphPosition) > 0.01f
                || !Mathf.IsEqualApprox(_boss.ActiveSlamTelegraph.Radius, _slamTelegraphRadius))
            {
                Fail("Boss crowd pressure changed locked Magma Slam origin");
                return;
            }
        }

        if (_boss.MagmaSlamImpactCount > _initialSlamImpactCount)
        {
            var expected = new Vector3(_slamCenter.X, 0.04f, _slamCenter.Z);
            if (_boss.LastSlamImpactCenter.DistanceTo(expected) > 0.01f
                || _boss.LastSlamImpactRadius != _slamTelegraphRadius)
            {
                Fail("Magma Slam impact did not use its locked center/radius");
                return;
            }

            NextStage();
            return;
        }

        FailIfTimedOut($"Boss did not complete locked Slam state={_boss.State}", 4.0);
    }

    private void PrepareSpearLock()
    {
        if (_boss.State != BrimstoneColossusState3D.Idle)
        {
            FailIfTimedOut($"Boss did not recover after Slam state={_boss.State}", 3.0);
            return;
        }

        if (!_spearPressurePlaced)
        {
            PlacePressureNeighbors(_boss.GlobalPosition);
            _initialSpearLaunchCount = _boss.FlameSpearLaunchCount;
            _spearPressurePlaced = true;
            NextStage();
        }
    }

    private void ObserveSpearLock()
    {
        if (_boss.State == BrimstoneColossusState3D.PreparingSpear
            && _boss.ActiveSpearTelegraph != null
            && _boss.CrowdAgent != null
            && _boss.CrowdAgent.NeighborCount >= 3)
        {
            if (!_spearSnapshotCaptured)
            {
                _spearPosition = _boss.GlobalPosition;
                _spearStart = _boss.ActiveSpearTelegraph.StartPosition;
                _spearEnd = _boss.ActiveSpearTelegraph.EndPosition;
                _spearDirection = _boss.ActiveSpearTelegraph.LockedDirection;
                _spearSnapshotCaptured = true;
                _player.GlobalPosition += new Vector3(0.0f, 0.0f, -2.0f);
            }

            var telegraph = _boss.ActiveSpearTelegraph;
            if (_boss.GlobalPosition.DistanceTo(_spearPosition) > 0.01f
                || telegraph.StartPosition.DistanceTo(_spearStart) > 0.01f
                || telegraph.EndPosition.DistanceTo(_spearEnd) > 0.01f
                || telegraph.LockedDirection.DistanceTo(_spearDirection) > 0.001f)
            {
                Fail("Boss crowd pressure changed locked Flame Spear telegraph");
                return;
            }
        }

        if (_boss.FlameSpearLaunchCount > _initialSpearLaunchCount)
        {
            if (_boss.LastSpearLaunchDirection.DistanceTo(_spearDirection) > 0.001f)
            {
                Fail("Flame Spear projectile did not reuse LockedSpearDirection");
                return;
            }

            ReleasePressureNeighbors();
            NextStage();
            return;
        }

        FailIfTimedOut($"Boss did not complete locked Spear state={_boss.State}", 4.0);
    }

    private void PreparePause()
    {
        if (_pauseStarted)
        {
            return;
        }

        _boss.GlobalPosition = new Vector3(-10.0f, 0.0f, 0.0f);
        _player.GlobalPosition = new Vector3(10.0f, 0.0f, 0.0f);
        if (_boss.State != BrimstoneColossusState3D.Chasing || !_boss.Navigation.HasPath)
        {
            FailIfTimedOut($"Boss did not resume navigation setup state={_boss.State}", 4.0);
            return;
        }

        _pauseStarted = true;
        _pausePosition = _boss.GlobalPosition;
        _pauseUpdateCount = _coordinator.UpdateCount;
        _pauseRepathCount = _boss.Navigation.RepathCount;
        _pauseStuckCount = _boss.Navigation.StuckDetectionCount;
        if (!_flow.RestoreState(GameFlowState.GameOver))
        {
            Fail("could not enter GameOver for Boss navigation pause test");
            return;
        }

        NextStage();
    }

    private void ObservePause()
    {
        if (_stageElapsed < 0.8)
        {
            return;
        }

        if (_boss.GlobalPosition.DistanceTo(_pausePosition) > 0.01f
            || _boss.Navigation.RepathCount != _pauseRepathCount
            || _boss.Navigation.StuckDetectionCount != _pauseStuckCount
            || _coordinator.UpdateCount != _pauseUpdateCount)
        {
            Fail("Boss navigation or crowd metrics advanced while paused");
            return;
        }

        _flow.RestoreState(GameFlowState.Playing);
        _pausePosition = _boss.GlobalPosition;
        NextStage();
    }

    private void ObserveResumeThenDeath()
    {
        if (_stageElapsed < 0.8)
        {
            return;
        }

        if (_boss.GlobalPosition.DistanceTo(_pausePosition) < 0.1f)
        {
            FailIfTimedOut("Boss did not resume navigation after GameOver", 3.0);
            return;
        }

        var registeredBeforeDeath = _coordinator.RegisteredAgentCount;
        var result = _boss.ApplyDamage(new DamageRequest(
            10000,
            DamageType.Physical,
            "boss_navigation_smoke",
            CombatFaction.Player));
        if (result.DamageApplied <= 0
            || _boss.IsAlive
            || _boss.CrowdAgent == null
            || _boss.CrowdAgent.IsActive
            || _coordinator.RegisteredAgentCount != registeredBeforeDeath - 1
            || _boss.CollisionLayer != 0
            || _boss.CollisionMask != 0
            || _boss.IsPhysicsProcessing()
            || GetTree().GetNodesInGroup("item_drops_3d").Count == 0)
        {
            Fail("Boss death did not unregister crowd, disable runtime, or spawn loot");
            return;
        }

        GD.Print(
            "BOSS_NAVIGATION_3D_PASS layers=true large_route=true small_narrow=true "
            + "slam_lock=true spear_lock=true pause=true resume=true death_unregister=true");
        GetTree().Quit(0);
    }

    private void PlacePressureNeighbors(Vector3 center)
    {
        var offsets = new[]
        {
            new Vector3(-0.25f, 0.0f, 0.0f),
            new Vector3(0.25f, 0.0f, 0.0f),
            new Vector3(0.0f, 0.0f, 0.3f),
        };
        for (var i = 0; i < _pressureFerals.Count; i++)
        {
            var feral = _pressureFerals[i];
            feral.SetPhysicsProcess(false);
            feral.GlobalPosition = center + offsets[i];
        }
    }

    private void ReleasePressureNeighbors()
    {
        for (var i = 0; i < _pressureFerals.Count; i++)
        {
            if (GodotObject.IsInstanceValid(_pressureFerals[i]))
            {
                _pressureFerals[i].SetPhysicsProcess(true);
            }
        }
    }

    private FeralController3D FindFeral()
    {
        for (var i = 0; i < _coordinator.RegisteredAgentCount; i++)
        {
            var feral = _coordinator.GetRegisteredAgent(i)?.GetParent<Node3D>() as FeralController3D;
            if (feral != null && feral.IsAlive)
            {
                return feral;
            }
        }

        return null;
    }

    private bool IsInsideObstacle(Vector3 position)
    {
        return IsInside(_arena.ObstacleA, position, 0.0f)
            || IsInside(_arena.ObstacleB, position, 0.0f);
    }

    private static bool PathContainsAabb(Vector3[] path, Aabb area)
    {
        return PathContainsAabb(path, area, 0.0f);
    }

    private static bool PathContainsAabb(Vector3[] path, Aabb area, float margin)
    {
        for (var i = 0; i < path.Length; i++)
        {
            if (IsInside(area, path[i], margin))
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatPath(Vector3[] path)
    {
        var values = new string[path.Length];
        for (var i = 0; i < path.Length; i++)
        {
            values[i] = $"({path[i].X:F1},{path[i].Z:F1})";
        }

        return string.Join("->", values);
    }

    private static string FormatNavigationMap(NavigationAgent3D agent)
    {
        if (agent == null)
        {
            return "null";
        }

        var map = agent.GetNavigationMap();
        return $"valid={map.IsValid} iteration={NavigationServer3D.MapGetIterationId(map)}";
    }

    private static bool IsInside(Aabb area, Vector3 position, float margin)
    {
        var min = area.Position - new Vector3(margin, 0.0f, margin);
        var max = area.End + new Vector3(margin, 0.0f, margin);
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

    private void FailIfTimedOut(string message, double timeoutSeconds)
    {
        if (_stageElapsed > timeoutSeconds)
        {
            Fail(message);
        }
    }

    private void Fail(string message)
    {
        GD.PushError($"BossNavigation3DRegressionSmoke FAIL: {message}");
        GetTree().Quit(1);
    }
}
