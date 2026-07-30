using System;
using System.Collections.Generic;
using System.Text;
using Godot;

/// <summary>
/// Stage 3C runtime contract for registration, symmetric crowd separation,
/// deterministic congestion recovery, locked attack states and lifecycle
/// cleanup at 24 and 40 dynamically spawned enemies.
/// </summary>
public partial class CrowdNavigation3DRegressionSmoke : Node
{
    private CrowdNavigationStressArena3D _arena;
    private EnemyCrowdCoordinator3D _coordinator;
    private PlayerController3D _player;
    private GameFlowController3D _flow;
    private FeralController3D _lockedFeral;
    private SpitterController3D _lockedSpitter;
    private FeralController3D _congestionFeralA;
    private FeralController3D _congestionFeralB;
    private Node3D _congestionBlocker;
    private readonly List<Node3D> _frozenNodes = new();
    private readonly List<Vector3> _frozenPositions = new();
    private readonly Dictionary<int, float> _distanceBaselines = new();
    private readonly Dictionary<int, bool> _checkpointPassed = new();
    private readonly Dictionary<int, Vector3> _pausedPositions = new();

    private int _stage;
    private double _stageElapsed;
    private double _totalElapsed;
    private int _initialSevereOverlapCount;
    private int _initialImpactCount;
    private int _initialShotCount;
    private int _initialFeralRecoveryCount;
    private int _initialSpitterRecoveryCount;
    private int _pauseUpdateCount;
    private int _pauseRepathCount;
    private int _pauseRecoveryCount;
    private Vector3 _feralWindupPosition;
    private Vector3 _feralTelegraphPosition;
    private Vector3 _spitterWindupPosition;
    private Vector3 _spitterTelegraphStart;
    private Vector3 _spitterTelegraphEnd;
    private Vector3 _spitterTelegraphDirection;
    private long _firstRecoveryFrame = -1;
    private long _secondRecoveryFrame = -1;
    private int _queueFreeUpdateBaseline;
    private bool _stageStarted;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _arena = GetNode<CrowdNavigationStressArena3D>("Arena3D");
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
        if (_totalElapsed > 75.0)
        {
            Fail($"timeout stage={_stage}");
            return;
        }

        if (_coordinator == null || _player == null)
        {
            Fail("arena did not expose coordinator and player");
            return;
        }

        switch (_stage)
        {
            case 0:
                WaitForRegistration();
                break;
            case 1:
                ObserveInitialSeparation();
                break;
            case 2:
                ObserveFeralLockedWindup();
                break;
            case 3:
                ObserveCrowdRoute();
                break;
            case 4:
                ObserveForcedCongestionRecovery();
                break;
            case 5:
                ObserveSpitterLockedWindup();
                break;
            case 6:
                ObservePauseFreeze();
                break;
            case 7:
                WaitForExpandedRegistration();
                break;
            case 8:
                ObserveFortyAgentPressure();
                break;
            case 9:
                ObserveUnregistration();
                break;
        }
    }

    private void WaitForRegistration()
    {
        if (_coordinator.RegisteredAgentCount != 24 || _arena.SpawnedEnemyCount != 24)
        {
            return;
        }

        var ids = new HashSet<int>();
        for (var i = 0; i < _coordinator.RegisteredAgentCount; i++)
        {
            var agent = _coordinator.GetRegisteredAgent(i);
            if (agent == null || !GodotObject.IsInstanceValid(agent) || !ids.Add(agent.CrowdId))
            {
                Fail("dynamic registration produced an invalid or duplicate CrowdId");
                return;
            }
        }

        _initialSevereOverlapCount = CountPairsUnderDistance(0.25f);
        if (_initialSevereOverlapCount <= 0)
        {
            Fail("initial overlap fixture did not contain a severe overlap pair");
            return;
        }

        NextStage();
    }

    private void ObserveInitialSeparation()
    {
        if (CountPairsUnderDistance(0.25f) < _initialSevereOverlapCount)
        {
            StartFeralLock();
            NextStage();
            return;
        }

        FailIfTimedOut(
            $"initial overlap did not decrease before timeout initial={_initialSevereOverlapCount} "
            + $"current={CountPairsUnderDistance(0.25f)}", 4.0);
    }

    private void StartFeralLock()
    {
        _lockedFeral = FindFeral();
        if (_lockedFeral == null)
        {
            Fail("no live Feral was registered for locked-state pressure test");
            return;
        }

        var lockPosition = _player.GlobalPosition + new Vector3(-1.0f, 0.0f, 0.0f);
        _lockedFeral.GlobalPosition = lockPosition;
        _lockedFeral.AttackCooldown = 0.0f;
        _lockedFeral.NavigationMovementSuppressed = false;
        FreezeNeighbors(_lockedFeral, lockPosition);
        _initialImpactCount = _lockedFeral.ImpactCount;
        _stageStarted = true;
    }

    private void ObserveFeralLockedWindup()
    {
        if (_lockedFeral == null || !GodotObject.IsInstanceValid(_lockedFeral))
        {
            Fail("locked Feral was disposed during windup");
            return;
        }

        if (_lockedFeral.State == FeralState3D.Windup)
        {
            if (!_stageStarted)
            {
                _stageStarted = true;
                _feralWindupPosition = _lockedFeral.GlobalPosition;
                _feralTelegraphPosition = _lockedFeral.ActiveTelegraph?.GlobalPosition ?? Vector3.Zero;
                if (_lockedFeral.CrowdAgent == null || _lockedFeral.CrowdAgent.NeighborCount < 2
                    || _lockedFeral.ActiveTelegraph == null)
                {
                    Fail("Feral Windup did not retain crowd pressure and telegraph");
                    return;
                }
            }

            if (_lockedFeral.GlobalPosition.DistanceTo(_feralWindupPosition) > 0.01f
                || _lockedFeral.ActiveTelegraph == null
                || _lockedFeral.ActiveTelegraph.GlobalPosition.DistanceTo(_feralTelegraphPosition) > 0.01f)
            {
                Fail("crowd pressure moved Feral or altered its telegraph during Windup");
                return;
            }
        }

        if (_lockedFeral.ImpactCount > _initialImpactCount)
        {
            ReleaseFrozenNeighbors();
            _lockedFeral.GlobalPosition = new Vector3(-10.5f, 0.0f, 0.0f);
            _stageStarted = false;
            StartCrowdRoute();
            NextStage();
            return;
        }

        FailIfTimedOut($"Feral did not reach locked Impact state={_lockedFeral.State}", 4.0);
    }

    private void StartCrowdRoute()
    {
        _distanceBaselines.Clear();
        _checkpointPassed.Clear();
        for (var i = 0; i < _coordinator.RegisteredAgentCount; i++)
        {
            var agent = _coordinator.GetRegisteredAgent(i);
            if (agent == null)
            {
                continue;
            }

            _distanceBaselines[agent.CrowdId] = HorizontalDistance(agent.WorldPosition, _player.GlobalPosition);
            _checkpointPassed[agent.CrowdId] = false;
        }
    }

    private void ObserveCrowdRoute()
    {
        var progressCount = 0;
        var checkpointCount = 0;
        for (var i = 0; i < _coordinator.RegisteredAgentCount; i++)
        {
            var agent = _coordinator.GetRegisteredAgent(i);
            if (agent == null || !GodotObject.IsInstanceValid(agent))
            {
                Fail("registered crowd agent became invalid during route test");
                return;
            }

            var actor = agent.GetParent<Node3D>();
            if (actor == null || IsInsideObstacle(actor.GlobalPosition))
            {
                Fail($"enemy entered a static obstacle during crowd route pos={actor?.GlobalPosition}");
                return;
            }

            if (_distanceBaselines.TryGetValue(agent.CrowdId, out var baseline)
                && HorizontalDistance(agent.WorldPosition, _player.GlobalPosition) <= baseline - 4.0f)
            {
                progressCount++;
            }

            if (actor.GlobalPosition.X > _arena.ChokeCheckpoint.X)
            {
                _checkpointPassed[agent.CrowdId] = true;
            }

            if (_checkpointPassed.TryGetValue(agent.CrowdId, out var passed) && passed)
            {
                checkpointCount++;
            }
        }

        if (progressCount >= 20 && checkpointCount >= 18)
        {
            StartForcedCongestion();
            NextStage();
            return;
        }

        FailIfTimedOut(
            $"crowd route progress={progressCount}/24 checkpoint={checkpointCount}/24 "
            + RouteSummary(), 18.0);
    }

    private void StartForcedCongestion()
    {
        _congestionFeralA = FindFeral(null, 0);
        _congestionFeralB = FindFeral(_congestionFeralA, 0);
        if (_congestionFeralA == null || _congestionFeralB == null)
        {
            Fail("could not select two Ferals for deterministic congestion recovery");
            return;
        }

        var basePosition = new Vector3(-3.0f, 0.0f, 0.0f);
        _congestionFeralA.GlobalPosition = basePosition;
        _congestionFeralB.GlobalPosition = basePosition + new Vector3(0.08f, 0.0f, 0.05f);
        _congestionFeralA.NavigationMovementSuppressed = true;
        _congestionFeralB.NavigationMovementSuppressed = true;
        _congestionFeralA.AttackCooldown = 100.0f;
        _congestionFeralB.AttackCooldown = 100.0f;

        _congestionBlocker = FindOtherActor(_congestionFeralA, _congestionFeralB);
        if (_congestionBlocker == null)
        {
            Fail("could not select a congestion blocker");
            return;
        }

        _congestionBlocker.GlobalPosition = basePosition + new Vector3(-0.04f, 0.0f, 0.08f);
        _congestionBlocker.SetPhysicsProcess(false);
        _initialFeralRecoveryCount = _congestionFeralA.CrowdAgent?.CongestionRecoveryCount ?? 0;
        _initialSpitterRecoveryCount = _congestionFeralB.CrowdAgent?.CongestionRecoveryCount ?? 0;
        _stageStarted = true;
    }

    private void ObserveForcedCongestionRecovery()
    {
        if (_congestionFeralA == null || _congestionFeralB == null)
        {
            Fail("forced congestion actors disappeared");
            return;
        }

        var recoveryA = _congestionFeralA.CrowdAgent?.CongestionRecoveryCount ?? 0;
        var recoveryB = _congestionFeralB.CrowdAgent?.CongestionRecoveryCount ?? 0;
        if (recoveryA > _initialFeralRecoveryCount && _firstRecoveryFrame < 0)
        {
            _firstRecoveryFrame = _congestionFeralA.CrowdAgent.LastRecoveryPhysicsFrame;
        }

        if (recoveryB > _initialSpitterRecoveryCount && _secondRecoveryFrame < 0)
        {
            _secondRecoveryFrame = _congestionFeralB.CrowdAgent.LastRecoveryPhysicsFrame;
        }

        var repathCount = (_congestionFeralA.Navigation?.RepathCount ?? 0)
            + (_congestionFeralB.Navigation?.RepathCount ?? 0);
        if (_firstRecoveryFrame >= 0 && _secondRecoveryFrame >= 0
            && Math.Abs(_firstRecoveryFrame - _secondRecoveryFrame) >= 2
            && repathCount > 0)
        {
            _congestionFeralA.NavigationMovementSuppressed = false;
            _congestionFeralB.NavigationMovementSuppressed = false;
            _congestionBlocker.SetPhysicsProcess(true);
            ReleaseFrozenNeighbors();
            StartSpitterLock();
            NextStage();
            return;
        }

        FailIfTimedOut(
            $"congestion recovery did not stagger A={recoveryA} B={recoveryB} "
            + $"frames={_firstRecoveryFrame}/{_secondRecoveryFrame}", 6.0);
    }

    private void StartSpitterLock()
    {
        _lockedSpitter = FindSpitter();
        if (_lockedSpitter == null)
        {
            Fail("no live Spitter was registered for locked-state pressure test");
            return;
        }

        var lockPosition = _player.GlobalPosition + new Vector3(-6.0f, 0.0f, 0.0f);
        _lockedSpitter.GlobalPosition = lockPosition;
        _lockedSpitter.AttackCooldown = 0.0f;
        _lockedSpitter.AimSeconds = 0.05f;
        _lockedSpitter.TelegraphSeconds = 0.35f;
        FreezeNeighbors(_lockedSpitter, lockPosition);
        _initialShotCount = _lockedSpitter.ProjectileShotCount;
        _stageStarted = false;
    }

    private void ObserveSpitterLockedWindup()
    {
        if (_lockedSpitter == null || !GodotObject.IsInstanceValid(_lockedSpitter))
        {
            Fail("locked Spitter was disposed during Windup");
            return;
        }

        if (_lockedSpitter.State == SpitterState3D.Windup)
        {
            if (!_stageStarted)
            {
                _stageStarted = true;
                _spitterWindupPosition = _lockedSpitter.GlobalPosition;
                var telegraph = _lockedSpitter.ActiveTelegraph;
                if (telegraph == null || _lockedSpitter.CrowdAgent == null
                    || _lockedSpitter.CrowdAgent.NeighborCount < 2)
                {
                    Fail("Spitter Windup did not retain crowd pressure and telegraph");
                    return;
                }

                _spitterTelegraphStart = telegraph.StartPosition;
                _spitterTelegraphEnd = telegraph.EndPosition;
                _spitterTelegraphDirection = telegraph.LockedDirection;
            }

            var activeTelegraph = _lockedSpitter.ActiveTelegraph;
            if (_lockedSpitter.GlobalPosition.DistanceTo(_spitterWindupPosition) > 0.01f
                || activeTelegraph == null
                || activeTelegraph.StartPosition.DistanceTo(_spitterTelegraphStart) > 0.01f
                || activeTelegraph.EndPosition.DistanceTo(_spitterTelegraphEnd) > 0.01f
                || activeTelegraph.LockedDirection.DistanceTo(_spitterTelegraphDirection) > 0.001f)
            {
                Fail("crowd pressure moved Spitter or changed its locked telegraph");
                return;
            }
        }

        if (_lockedSpitter.ProjectileShotCount > _initialShotCount)
        {
            if (_lockedSpitter.LastLaunchDirection.DistanceTo(_spitterTelegraphDirection) > 0.001f)
            {
                Fail("Spitter projectile direction did not reuse the locked telegraph direction");
                return;
            }

            ReleaseFrozenNeighbors();
            StartPauseTest();
            NextStage();
            return;
        }

        FailIfTimedOut($"Spitter did not launch after locked Windup state={_lockedSpitter.State}", 5.0);
    }

    private void StartPauseTest()
    {
        _pausedPositions.Clear();
        _pauseUpdateCount = _coordinator.UpdateCount;
        _pauseRepathCount = SumRepathCounts();
        _pauseRecoveryCount = SumRecoveryCounts();
        for (var i = 0; i < _coordinator.RegisteredAgentCount; i++)
        {
            var agent = _coordinator.GetRegisteredAgent(i);
            if (agent != null)
            {
                _pausedPositions[agent.CrowdId] = agent.WorldPosition;
            }
        }

        if (!_flow.RestoreState(GameFlowState.GameOver))
        {
            Fail("could not enter GameOver for pause contract");
        }
    }

    private void ObservePauseFreeze()
    {
        if (_stageElapsed < 0.8)
        {
            return;
        }

        if (_coordinator.UpdateCount != _pauseUpdateCount
            || SumRepathCounts() != _pauseRepathCount
            || SumRecoveryCounts() != _pauseRecoveryCount)
        {
            Fail("crowd coordinator, repath, or recovery metrics advanced while paused");
            return;
        }

        for (var i = 0; i < _coordinator.RegisteredAgentCount; i++)
        {
            var agent = _coordinator.GetRegisteredAgent(i);
            if (agent != null && _pausedPositions.TryGetValue(agent.CrowdId, out var position)
                && agent.WorldPosition.DistanceTo(position) > 0.01f)
            {
                Fail("enemy position changed while GameOver was paused");
                return;
            }
        }

        _flow.RestoreState(GameFlowState.Playing);
        _arena.SpawnAdditionalWave(12, 4);
        NextStage();
    }

    private void WaitForExpandedRegistration()
    {
        if (_coordinator.RegisteredAgentCount == 40 && _arena.SpawnedEnemyCount == 40)
        {
            NextStage();
            return;
        }

        FailIfTimedOut($"expanded wave registration={_coordinator.RegisteredAgentCount}/40", 4.0);
    }

    private void ObserveFortyAgentPressure()
    {
        if (_coordinator.PairEvaluationCount > 780)
        {
            Fail($"pair evaluation exceeded n(n-1)/2 bound: {_coordinator.PairEvaluationCount}");
            return;
        }

        for (var i = 0; i < _coordinator.RegisteredAgentCount; i++)
        {
            var agent = _coordinator.GetRegisteredAgent(i);
            if (agent == null || !GodotObject.IsInstanceValid(agent)
                || agent.GetParent<Node3D>() == null)
            {
                Fail("40-agent pressure pass retained a disposed or detached reference");
                return;
            }
        }

        if (_stageElapsed >= 4.5)
        {
            QueueTenEnemiesForFree();
            NextStage();
        }
    }

    private void QueueTenEnemiesForFree()
    {
        _queueFreeUpdateBaseline = _coordinator.UpdateCount;
        for (var i = 0; i < 10 && i < _coordinator.RegisteredAgentCount; i++)
        {
            var agent = _coordinator.GetRegisteredAgent(i);
            agent?.GetParent<Node3D>()?.QueueFree();
        }
    }

    private void ObserveUnregistration()
    {
        if (_coordinator.RegisteredAgentCount == 30
            && _coordinator.UpdateCount > _queueFreeUpdateBaseline
            && _coordinator.PairEvaluationCount <= 435)
        {
            FinishSuccess();
            return;
        }

        FailIfTimedOut(
            $"QueueFree did not unregister exactly ten agents registered={_coordinator.RegisteredAgentCount} "
            + $"pairs={_coordinator.PairEvaluationCount} updates={_coordinator.UpdateCount} "
            + $"baseline={_queueFreeUpdateBaseline} paused={GetTree().Paused}", 3.0);
    }

    private void FreezeNeighbors(Node3D selected, Vector3 center)
    {
        ReleaseFrozenNeighbors();
        for (var i = 0; i < _coordinator.RegisteredAgentCount && _frozenNodes.Count < 3; i++)
        {
            var agent = _coordinator.GetRegisteredAgent(i);
            var actor = agent?.GetParent<Node3D>();
            if (actor == null || actor == selected || !GodotObject.IsInstanceValid(actor))
            {
                continue;
            }

            _frozenNodes.Add(actor);
            _frozenPositions.Add(actor.GlobalPosition);
            actor.GlobalPosition = center + new Vector3(
                (_frozenNodes.Count - 2) * 0.12f,
                0.0f,
                (_frozenNodes.Count - 1) * 0.09f);
            actor.SetPhysicsProcess(false);
        }
    }

    private void ReleaseFrozenNeighbors()
    {
        for (var i = 0; i < _frozenNodes.Count; i++)
        {
            var actor = _frozenNodes[i];
            if (GodotObject.IsInstanceValid(actor))
            {
                actor.GlobalPosition = _frozenPositions[i];
                actor.SetPhysicsProcess(true);
            }
        }

        _frozenNodes.Clear();
        _frozenPositions.Clear();
    }

    private FeralController3D FindFeral(FeralController3D excluded = null, int ordinal = 0)
    {
        var found = 0;
        for (var i = 0; i < _coordinator.RegisteredAgentCount; i++)
        {
            var feral = _coordinator.GetRegisteredAgent(i)?.GetParent<Node3D>() as FeralController3D;
            if (feral == null || feral == excluded || !feral.IsAlive)
            {
                continue;
            }

            if (found++ >= ordinal)
            {
                return feral;
            }
        }

        return null;
    }

    private SpitterController3D FindSpitter()
    {
        for (var i = 0; i < _coordinator.RegisteredAgentCount; i++)
        {
            var spitter = _coordinator.GetRegisteredAgent(i)?.GetParent<Node3D>() as SpitterController3D;
            if (spitter != null && spitter.IsAlive)
            {
                return spitter;
            }
        }

        return null;
    }

    private Node3D FindOtherActor(Node3D first, Node3D second)
    {
        for (var i = 0; i < _coordinator.RegisteredAgentCount; i++)
        {
            var actor = _coordinator.GetRegisteredAgent(i)?.GetParent<Node3D>();
            if (actor != null && actor != first && actor != second && GodotObject.IsInstanceValid(actor))
            {
                return actor;
            }
        }

        return null;
    }

    private int CountPairsUnderDistance(float distance)
    {
        var count = 0;
        for (var i = 0; i < _coordinator.RegisteredAgentCount; i++)
        {
            var first = _coordinator.GetRegisteredAgent(i);
            if (first == null)
            {
                continue;
            }

            for (var j = i + 1; j < _coordinator.RegisteredAgentCount; j++)
            {
                var second = _coordinator.GetRegisteredAgent(j);
                if (second != null && HorizontalDistance(first.WorldPosition, second.WorldPosition) < distance)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private int SumRepathCounts()
    {
        var total = 0;
        for (var i = 0; i < _coordinator.RegisteredAgentCount; i++)
        {
            var actor = _coordinator.GetRegisteredAgent(i)?.GetParent<Node3D>();
            total += actor switch
            {
                FeralController3D feral => feral.Navigation?.RepathCount ?? 0,
                SpitterController3D spitter => spitter.Navigation?.RepathCount ?? 0,
                _ => 0,
            };
        }

        return total;
    }

    private int SumRecoveryCounts()
    {
        var total = 0;
        for (var i = 0; i < _coordinator.RegisteredAgentCount; i++)
        {
            total += _coordinator.GetRegisteredAgent(i)?.CongestionRecoveryCount ?? 0;
        }

        return total;
    }

    private string RouteSummary()
    {
        var summary = new StringBuilder("positions=");
        var limit = Mathf.Min(6, _coordinator.RegisteredAgentCount);
        for (var i = 0; i < limit; i++)
        {
            var agent = _coordinator.GetRegisteredAgent(i);
            var actor = agent?.GetParent<Node3D>();
            summary.Append(actor?.GlobalPosition.ToString() ?? "<null>");
            if (actor is FeralController3D feral)
            {
                summary.Append($"/F:{feral.Navigation?.PathPointCount}/{feral.Navigation?.PathLength:0.0}");
            }
            else if (actor is SpitterController3D spitter)
            {
                summary.Append($"/S:{spitter.Navigation?.PathPointCount}/{spitter.Navigation?.PathLength:0.0}");
            }

            if (i + 1 < limit)
            {
                summary.Append(';');
            }
        }

        return summary.ToString();
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
        _stageStarted = false;
    }

    private void FailIfTimedOut(string message, double timeoutSeconds)
    {
        if (_stageElapsed > timeoutSeconds)
        {
            Fail(message);
        }
    }

    private void FinishSuccess()
    {
        GD.Print(
            $"CROWD_NAVIGATION_3D_PASS registered24=true separated=true route=true "
            + $"congestion=true pause=true registered40=true pairBound=true unregistered30=true");
        GetTree().Quit(0);
    }

    private void Fail(string message)
    {
        GD.PushError($"CrowdNavigation3DRegressionSmoke FAIL: {message}");
        GetTree().Quit(1);
    }
}
