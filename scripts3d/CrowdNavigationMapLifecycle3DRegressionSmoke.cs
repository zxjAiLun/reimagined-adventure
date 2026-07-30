using System.Collections.Generic;
using Godot;

/// <summary>
/// Verifies that overlapping old/new map lifetimes never cross-bind a dynamic
/// enemy to the other map's crowd coordinator.
/// </summary>
public partial class CrowdNavigationMapLifecycle3DRegressionSmoke : Node
{
    [Export] public PackedScene ArenaScene { get; set; }

    private CrowdNavigationStressArena3D _mapA;
    private CrowdNavigationStressArena3D _mapB;
    private EnemyCrowdCoordinator3D _coordinatorA;
    private EnemyCrowdCoordinator3D _coordinatorB;
    private readonly List<Vector3> _initialPositions = new();
    private int _stage;
    private double _stageElapsed;
    private double _totalElapsed;
    private int _updateBaselineAfterQueueFree;
    private bool _correctionObserved;
    private bool _pathObserved;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        if (ArenaScene == null)
        {
            Fail("ArenaScene was not configured");
            return;
        }

        _mapA = CreateEmptyMap("MapA");
        _mapB = CreateEmptyMap("MapB");
        _coordinatorA = _mapA.Coordinator;
        _coordinatorB = _mapB.Coordinator;

        // Keep the test's two player groups unambiguous while both maps are
        // alive. The old map is a lifecycle fixture, not an active combat map.
        _mapA.Player.RemoveFromGroup("player_3d");
        _mapB.Player.GetNode<HealthComponent>("HealthComponent").SetMaxHealth(100000);
    }

    public override void _Process(double delta)
    {
        _totalElapsed += delta;
        _stageElapsed += delta;
        if (_totalElapsed > 12.0)
        {
            Fail($"timeout stage={_stage}");
            return;
        }

        switch (_stage)
        {
            case 0:
                SpawnMapBEnemies();
                break;
            case 1:
                VerifyMapScopedRegistration();
                break;
            case 2:
                VerifyOldMapRemoval();
                break;
        }
    }

    private CrowdNavigationStressArena3D CreateEmptyMap(string name)
    {
        var map = ArenaScene.Instantiate<CrowdNavigationStressArena3D>();
        map.Name = name;
        map.FeralCount = 0;
        map.SpitterCount = 0;
        AddChild(map);
        return map;
    }

    private void SpawnMapBEnemies()
    {
        if (_coordinatorA == null || _coordinatorB == null || _mapB.SpawnedEnemyCount != 0)
        {
            return;
        }

        _mapB.SpawnAdditionalWave(2, 0);
        NextStage();
    }

    private void VerifyMapScopedRegistration()
    {
        if (_coordinatorB.RegisteredAgentCount != 2)
        {
            FailIfTimedOut($"Map B registration={_coordinatorB.RegisteredAgentCount}/2", 4.0);
            return;
        }

        if (_coordinatorA.RegisteredAgentCount != 0)
        {
            Fail($"Map A received Map B agents count={_coordinatorA.RegisteredAgentCount}");
            return;
        }

        var ids = new HashSet<int>();
        _initialPositions.Clear();
        for (var i = 0; i < _coordinatorB.RegisteredAgentCount; i++)
        {
            var agent = _coordinatorB.GetRegisteredAgent(i);
            var actor = agent?.GetParent<Node3D>();
            if (agent == null || actor == null || !agent.IsRegisteredWith(_coordinatorB)
                || agent.IsRegisteredWith(_coordinatorA) || !ids.Add(agent.CrowdId))
            {
                Fail("Map B agent was not bound exclusively to Coordinator B");
                return;
            }

            _initialPositions.Add(actor.GlobalPosition);
        }

        _mapA.ProcessMode = ProcessModeEnum.Disabled;
        _mapA.QueueFree();
        _updateBaselineAfterQueueFree = _coordinatorB.UpdateCount;
        NextStage();
    }

    private void VerifyOldMapRemoval()
    {
        for (var i = 0; i < _coordinatorB.RegisteredAgentCount; i++)
        {
            var agent = _coordinatorB.GetRegisteredAgent(i);
            var actor = agent?.GetParent<Node3D>();
            if (agent == null || actor == null || !agent.IsRegisteredWith(_coordinatorB)
                || !GodotObject.IsInstanceValid(actor))
            {
                Fail("Map B agent lost its coordinator after Map A QueueFree");
                return;
            }

            if (agent.NeighborCount > 0 && agent.SeparationVelocity.LengthSquared() > 0.0001f)
            {
                _correctionObserved = true;
            }

            if (actor is FeralController3D feral
                && feral.Navigation != null
                && feral.Navigation.IsNavigationReady
                && feral.Navigation.HasPath)
            {
                _pathObserved = true;
            }
        }

        if (_coordinatorB.UpdateCount > _updateBaselineAfterQueueFree
            && _correctionObserved
            && _pathObserved
            && AnyMapBEnemyMoved())
        {
            GD.Print(
                "CROWD_NAVIGATION_MAP_LIFECYCLE_3D_PASS scoped=true old_map_removed=true "
                + "new_map_updates=true navigation=true correction=true");
            GetTree().Quit(0);
            return;
        }

        FailIfTimedOut(
            $"Map B did not survive Map A removal updates={_coordinatorB.UpdateCount}/"
            + $"{_updateBaselineAfterQueueFree} correction={_correctionObserved} "
            + $"path={_pathObserved} moved={AnyMapBEnemyMoved()}", 4.0);
    }

    private bool AnyMapBEnemyMoved()
    {
        for (var i = 0; i < _coordinatorB.RegisteredAgentCount; i++)
        {
            var agent = _coordinatorB.GetRegisteredAgent(i);
            var actor = agent?.GetParent<Node3D>();
            if (actor != null && i < _initialPositions.Count
                && actor.GlobalPosition.DistanceTo(_initialPositions[i]) > 0.05f)
            {
                return true;
            }
        }

        return false;
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
        GD.PushError($"CrowdNavigationMapLifecycle3DRegressionSmoke FAIL: {message}");
        GetTree().Quit(1);
    }
}
