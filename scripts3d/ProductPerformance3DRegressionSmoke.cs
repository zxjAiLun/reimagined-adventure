using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Bounded product-runtime performance baseline. It uses the formal map and 40
/// live navigation actors; thresholds are intentionally conservative for CI.
/// </summary>
public partial class ProductPerformance3DRegressionSmoke : Node
{
    private enum Stage { Bind, ReleaseEncounter, SpawnPressure, Warmup, Sample }

    private const int AgentCount = 40;
    private Stage _stage;
    private double _elapsed;
    private bool _complete;
    private RunSessionNode _run;
    private TestArena3D _arena;
    private Node3D _enemies;
    private EncounterDirector3D _director;
    private EnemyCrowdCoordinator3D _coordinator;
    private readonly Dictionary<ulong, Vector3> _initialPositions = new();
    private int _samples;
    private double _processSecondsTotal;
    private double _physicsSecondsTotal;
    private double _maxCombinedSeconds;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _run = GetNodeOrNull<RunSessionNode>("RunShell3D");
        if (_run == null) Fail("performance smoke is missing RunShell3D");
    }

    public override void _Process(double delta)
    {
        if (_complete) return;
        _elapsed += delta;
        if (_elapsed > 30.0)
        {
            Fail($"performance smoke timed out at {_stage}");
            return;
        }

        switch (_stage)
        {
            case Stage.Bind: Bind(); break;
            case Stage.ReleaseEncounter: ReleaseEncounter(); break;
            case Stage.SpawnPressure: SpawnPressure(); break;
            case Stage.Warmup: Warmup(); break;
            case Stage.Sample: Sample(); break;
        }
    }

    private void Bind()
    {
        _arena ??= _run.CurrentMap3D;
        _enemies ??= _arena?.GetNodeOrNull<Node3D>("EnemyContainer");
        _director ??= _arena?.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        _coordinator ??= _arena?.GetNodeOrNull<EnemyCrowdCoordinator3D>("EnemyCrowdCoordinator3D");
        if (_arena == null || _enemies == null || _director == null || _coordinator == null)
        {
            return;
        }

        _director.Enabled = false;
        _director.ProcessMode = ProcessModeEnum.Disabled;
        foreach (var child in _enemies.GetChildren()) child.QueueFree();
        Advance(Stage.ReleaseEncounter);
    }

    private void ReleaseEncounter()
    {
        if (_enemies.GetChildCount() > 0 || _coordinator.RegisteredAgentCount > 0)
        {
            return;
        }
        Advance(Stage.SpawnPressure);
    }

    private void SpawnPressure()
    {
        var feralScene = GD.Load<PackedScene>("res://scenes3d/Feral3D.tscn");
        var spitterScene = GD.Load<PackedScene>("res://scenes3d/Spitter3D.tscn");
        if (feralScene == null || spitterScene == null)
        {
            Fail("performance pressure scenes could not load");
            return;
        }

        for (var index = 0; index < AgentCount; index++)
        {
            var actor = index < AgentCount / 2
                ? CreateFeral(feralScene, index)
                : CreateSpitter(spitterScene, index);
            _enemies.AddChild(actor);
        }
        Advance(Stage.Warmup);
    }

    private static Node3D CreateFeral(PackedScene scene, int index)
    {
        var feral = scene.Instantiate<FeralController3D>();
        feral.Name = $"PerformanceFeral{index:00}";
        feral.ContactDamage = 0;
        feral.Position = SpawnPosition(index);
        return feral;
    }

    private static Node3D CreateSpitter(PackedScene scene, int index)
    {
        var spitter = scene.Instantiate<SpitterController3D>();
        spitter.Name = $"PerformanceSpitter{index:00}";
        spitter.ProjectileDamage = 0;
        spitter.Position = SpawnPosition(index);
        return spitter;
    }

    private static Vector3 SpawnPosition(int index)
    {
        var column = index % 8;
        var row = index / 8;
        return new Vector3((column - 3.5f) * 2.45f, 0.0f, (row - 2.0f) * 2.55f);
    }

    private void Warmup()
    {
        if (_coordinator.RegisteredAgentCount != AgentCount)
        {
            return;
        }
        if (_initialPositions.Count == 0)
        {
            foreach (var actor in PressureActors())
            {
                _initialPositions[actor.GetInstanceId()] = actor.GlobalPosition;
            }
            _coordinator.ResetPhysicsTimingMetrics();
            _elapsed = 0.0;
        }
        if (_elapsed < 1.5) return;
        _samples = 0;
        _processSecondsTotal = 0.0;
        _physicsSecondsTotal = 0.0;
        _maxCombinedSeconds = 0.0;
        Advance(Stage.Sample);
    }

    private void Sample()
    {
        var process = Performance.GetMonitor(Performance.Monitor.TimeProcess);
        var physics = Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess);
        var combined = process + physics;
        _samples++;
        _processSecondsTotal += process;
        _physicsSecondsTotal += physics;
        _maxCombinedSeconds = Math.Max(_maxCombinedSeconds, combined);
        if (_elapsed < 5.0) return;

        var actors = PressureActors();
        var moved = actors.Count(actor => _initialPositions.TryGetValue(actor.GetInstanceId(), out var start)
            && actor.GlobalPosition.DistanceTo(start) >= 0.35f);
        var averageCombined = (_processSecondsTotal + _physicsSecondsTotal) / Math.Max(1, _samples);
        var expectedPairs = AgentCount * (AgentCount - 1) / 2;
        if (actors.Length != AgentCount
            || _coordinator.RegisteredAgentCount != AgentCount
            || _coordinator.PairEvaluationCount <= 0
            || _coordinator.PairEvaluationCount > expectedPairs
            || moved < 24
            || averageCombined > 0.04
            || _maxCombinedSeconds > 0.20
            || _coordinator.AveragePhysicsMilliseconds > 5.0
            || _coordinator.MaxPhysicsMilliseconds > 20.0)
        {
            Fail(
                $"performance baseline failed actors={actors.Length} registered={_coordinator.RegisteredAgentCount} "
                + $"pairs={_coordinator.PairEvaluationCount}/{expectedPairs} moved={moved} "
                + $"avg_frame_ms={averageCombined * 1000.0:0.00} max_frame_ms={_maxCombinedSeconds * 1000.0:0.00} "
                + $"crowd_avg_ms={_coordinator.AveragePhysicsMilliseconds:0.00} crowd_max_ms={_coordinator.MaxPhysicsMilliseconds:0.00}");
            return;
        }

        _complete = true;
        GD.Print(
            $"PRODUCT_PERFORMANCE_3D_REGRESSION_PASS actors=40 moved={moved} pairs={_coordinator.PairEvaluationCount}/{expectedPairs} "
            + $"avg_frame_ms={averageCombined * 1000.0:0.00} max_frame_ms={_maxCombinedSeconds * 1000.0:0.00} "
            + $"crowd_avg_ms={_coordinator.AveragePhysicsMilliseconds:0.00} crowd_max_ms={_coordinator.MaxPhysicsMilliseconds:0.00} "
            + $"fps={Engine.GetFramesPerSecond():0.0}");
        GetTree().Quit();
    }

    private void Advance(Stage stage)
    {
        _stage = stage;
        _elapsed = 0.0;
    }

    private Node3D[] PressureActors() => _enemies.GetChildren()
        .OfType<Node3D>()
        .Where(actor => actor is FeralController3D or SpitterController3D)
        .ToArray();

    private void Fail(string reason)
    {
        if (_complete) return;
        _complete = true;
        GetTree().Paused = false;
        Engine.TimeScale = 1.0;
        GD.PushError($"PRODUCT_PERFORMANCE_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}
