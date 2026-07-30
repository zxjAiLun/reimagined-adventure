using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Stage 3D pressure baseline. It runs the same saved dual-layer arena at
/// 24, 40 and 40-plus-Boss agents, checking pair-work bounds, references,
/// obstacle safety, movement progress and pause freezing.
/// </summary>
public partial class NavigationPressureBaseline3DRegressionSmoke : Node
{
    private BossNavigationStressArena3D _arena;
    private EnemyCrowdCoordinator3D _coordinator;
    private PlayerController3D _player;
    private GameFlowController3D _flow;
    private BrimstoneColossusController3D _boss;
    private readonly Dictionary<int, Vector3> _baselines = new();

    private int _stage;
    private double _stageElapsed;
    private double _totalElapsed;
    private int _expectedAgents;
    private int _maxPairEvaluations;
    private int _maxSevereOverlaps;
    private int _pauseUpdateCount;
    private int _pausePairCount;
    private int _pauseRepathCount;
    private int _pauseRecoveryCount;
    private Vector3 _pauseBossPosition;

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
        if (_totalElapsed > 45.0)
        {
            Fail($"timeout stage={_stage}");
            return;
        }

        switch (_stage)
        {
            case 0:
                WaitForTwentyFourAgents();
                break;
            case 1:
                ObserveTwentyFourAgents();
                break;
            case 2:
                WaitForFortyAgents();
                break;
            case 3:
                ObserveFortyAgents();
                break;
            case 4:
                WaitForBoss();
                break;
            case 5:
                ObserveBossPressure();
                break;
            case 6:
                ObservePauseFreeze();
                break;
        }
    }

    private void WaitForTwentyFourAgents()
    {
        if (!HasExpectedRegistry(24) || _arena.SpawnedEnemyCount != 24)
        {
            FailIfTimedOut($"stage A registration={_coordinator?.RegisteredAgentCount}/24", 6.0);
            return;
        }

        _expectedAgents = 24;
        CaptureBaselines();
        ResetMetrics();
        NextStage();
    }

    private void ObserveTwentyFourAgents()
    {
        var metrics = CollectMetrics();
        if (!metrics.Valid)
        {
            Fail(metrics.Error);
            return;
        }

        TrackMetrics();
        if (_stageElapsed < 4.0)
        {
            return;
        }

        if (!ValidatePressureStage("A", 24, metrics, minimumProgress: 16))
        {
            return;
        }

        GD.Print(FormatMetrics("A", metrics));
        _arena.SpawnAdditionalWave(12, 4);
        _stageElapsed = 0.0;
        _stage = 2;
    }

    private void WaitForFortyAgents()
    {
        if (!HasExpectedRegistry(40) || _arena.SpawnedEnemyCount != 40)
        {
            FailIfTimedOut($"stage B registration={_coordinator?.RegisteredAgentCount}/40", 6.0);
            return;
        }

        _expectedAgents = 40;
        CaptureBaselines();
        ResetMetrics();
        NextStage();
    }

    private void ObserveFortyAgents()
    {
        var metrics = CollectMetrics();
        if (!metrics.Valid)
        {
            Fail(metrics.Error);
            return;
        }

        TrackMetrics();
        if (_stageElapsed < 4.0)
        {
            return;
        }

        if (!ValidatePressureStage("B", 40, metrics, minimumProgress: 28))
        {
            return;
        }

        GD.Print(FormatMetrics("B", metrics));
        _boss = _arena.SpawnBossNow();
        _stageElapsed = 0.0;
        _stage = 4;
    }

    private void WaitForBoss()
    {
        if (_boss == null || !GodotObject.IsInstanceValid(_boss)
            || !HasExpectedRegistry(41) || _arena.SpawnedEnemyCount != 41)
        {
            FailIfTimedOut($"stage C registration={_coordinator?.RegisteredAgentCount}/41", 6.0);
            return;
        }

        _expectedAgents = 41;
        CaptureBaselines();
        ResetMetrics();
        NextStage();
    }

    private void ObserveBossPressure()
    {
        var metrics = CollectMetrics();
        if (!metrics.Valid)
        {
            Fail(metrics.Error);
            return;
        }

        TrackMetrics();
        if (_stageElapsed < 4.0)
        {
            return;
        }

        if (!ValidatePressureStage("C", 41, metrics, minimumProgress: 28))
        {
            return;
        }

        var bossNavigation = _boss.Navigation;
        if (bossNavigation == null || !bossNavigation.IsNavigationReady
            || !bossNavigation.HasPath || bossNavigation.PathPointCount < 3
            || bossNavigation.IsStuck)
        {
            Fail("Boss lost its large-agent navigation path during pressure baseline");
            return;
        }

        GD.Print(FormatMetrics("C", metrics)
            + $" bossPath={bossNavigation?.PathPointCount ?? 0}"
            + $" bossRepath={bossNavigation?.RepathCount ?? 0}");

        _pauseUpdateCount = _coordinator.UpdateCount;
        _pausePairCount = _coordinator.PairEvaluationCount;
        _pauseRepathCount = SumRepathCounts();
        _pauseRecoveryCount = SumRecoveryCounts();
        _pauseBossPosition = _boss.GlobalPosition;
        if (!_flow.RestoreState(GameFlowState.GameOver))
        {
            Fail("could not enter GameOver during pressure pause baseline");
            return;
        }

        NextStage();
    }

    private void ObservePauseFreeze()
    {
        if (_stageElapsed < 0.8)
        {
            return;
        }

        if (_coordinator.UpdateCount != _pauseUpdateCount
            || _coordinator.PairEvaluationCount != _pausePairCount
            || SumRepathCounts() != _pauseRepathCount
            || SumRecoveryCounts() != _pauseRecoveryCount
            || _boss.GlobalPosition.DistanceTo(_pauseBossPosition) > 0.01f)
        {
            Fail("navigation pressure metrics advanced while GameOver was paused");
            return;
        }

        _flow.RestoreState(GameFlowState.Playing);
        GD.Print(
            "NAVIGATION_PRESSURE_BASELINE_3D_PASS A=24 B=40 C=40+Boss "
            + $"maxPairs={_maxPairEvaluations} maxSevereOverlap={_maxSevereOverlaps} pause=true");
        GetTree().Quit(0);
    }

    private bool HasExpectedRegistry(int expected)
    {
        return _coordinator != null && _coordinator.RegisteredAgentCount == expected;
    }

    private void CaptureBaselines()
    {
        _baselines.Clear();
        for (var i = 0; i < _coordinator.RegisteredAgentCount; i++)
        {
            var agent = _coordinator.GetRegisteredAgent(i);
            if (agent != null && GodotObject.IsInstanceValid(agent))
            {
                _baselines[agent.CrowdId] = agent.WorldPosition;
            }
        }
    }

    private void ResetMetrics()
    {
        _maxPairEvaluations = 0;
        _maxSevereOverlaps = 0;
    }

    private void TrackMetrics()
    {
        _maxPairEvaluations = Mathf.Max(_maxPairEvaluations, _coordinator.PairEvaluationCount);
        _maxSevereOverlaps = Mathf.Max(_maxSevereOverlaps, _coordinator.SevereOverlapPairCount);
    }

    private PressureMetrics CollectMetrics()
    {
        var metrics = new PressureMetrics();
        if (!HasExpectedRegistry(_expectedAgents))
        {
            metrics.Error = $"registry changed during stage expected={_expectedAgents} "
                + $"actual={_coordinator?.RegisteredAgentCount}";
            return metrics;
        }

        for (var i = 0; i < _coordinator.RegisteredAgentCount; i++)
        {
            var agent = _coordinator.GetRegisteredAgent(i);
            if (agent == null || !GodotObject.IsInstanceValid(agent)
                || agent.GetParent<Node3D>() == null)
            {
                metrics.Error = "registered crowd agent became invalid";
                return metrics;
            }

            var actor = agent.GetParent<Node3D>();
            if (IsInsideObstacle(actor.GlobalPosition))
            {
                metrics.Error = $"actor entered static obstacle pos={actor.GlobalPosition}";
                return metrics;
            }

            if (_baselines.TryGetValue(agent.CrowdId, out var baseline)
                && HorizontalDistance(actor.GlobalPosition, _player.GlobalPosition)
                    <= HorizontalDistance(baseline, _player.GlobalPosition) - 0.5f)
            {
                metrics.ProgressCount++;
            }

            var navigation = actor.GetNodeOrNull<EnemyNavigation3D>("EnemyNavigation3D");
            if (navigation != null && navigation.HasPath)
            {
                metrics.PathCount++;
            }

            if (navigation != null && navigation.IsStuck)
            {
                metrics.StuckCount++;
            }

            if (agent.IsCongested)
            {
                metrics.CongestedCount++;
            }
        }

        metrics.Valid = true;
        metrics.PairEvaluations = _coordinator.PairEvaluationCount;
        metrics.SevereOverlaps = _coordinator.SevereOverlapPairCount;
        return metrics;
    }

    private bool ValidatePressureStage(string label, int expected, PressureMetrics metrics, int minimumProgress)
    {
        var pairLimit = expected * (expected - 1) / 2;
        if (metrics.PairEvaluations > pairLimit)
        {
            Fail($"stage {label} pair pass exceeded bound {metrics.PairEvaluations}/{pairLimit}");
            return false;
        }

        if (metrics.ProgressCount + metrics.PathCount < minimumProgress)
        {
            Fail($"stage {label} insufficient navigation progress "
                + $"progress={metrics.ProgressCount} paths={metrics.PathCount}/{expected}");
            return false;
        }

        if (metrics.StuckCount > 0 || metrics.CongestedCount > 0)
        {
            Fail($"stage {label} left permanent blocked agents "
                + $"stuck={metrics.StuckCount} congested={metrics.CongestedCount}");
            return false;
        }

        return true;
    }

    private string FormatMetrics(string label, PressureMetrics metrics)
    {
        return $"NAVIGATION_PRESSURE_{label} agents={_expectedAgents}"
            + $" pairEvaluations={metrics.PairEvaluations} severe={metrics.SevereOverlaps}"
            + $" progress={metrics.ProgressCount} paths={metrics.PathCount}"
            + $" stuck={metrics.StuckCount} congested={metrics.CongestedCount}";
    }

    private int SumRepathCounts()
    {
        var total = 0;
        for (var i = 0; i < _coordinator.RegisteredAgentCount; i++)
        {
            var actor = _coordinator.GetRegisteredAgent(i)?.GetParent<Node3D>();
            total += actor?.GetNodeOrNull<EnemyNavigation3D>("EnemyNavigation3D")?.RepathCount ?? 0;
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

    private bool IsInsideObstacle(Vector3 position)
    {
        return IsInside(_arena.ObstacleA, position) || IsInside(_arena.ObstacleB, position);
    }

    private static bool IsInside(Aabb area, Vector3 position)
    {
        return position.X >= area.Position.X && position.X <= area.End.X
            && position.Z >= area.Position.Z && position.Z <= area.End.Z;
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
        GD.PushError($"NavigationPressureBaseline3DRegressionSmoke FAIL: {message}");
        GetTree().Quit(1);
    }

    private sealed class PressureMetrics
    {
        public bool Valid;
        public string Error = string.Empty;
        public int PairEvaluations;
        public int SevereOverlaps;
        public int ProgressCount;
        public int PathCount;
        public int StuckCount;
        public int CongestedCount;
    }
}
