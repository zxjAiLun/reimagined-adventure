using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Owns the one symmetric pair pass used by enemy crowd agents.
/// The coordinator deliberately does not move actors; it only produces a
/// per-agent separation correction and pressure measurement.
/// </summary>
public partial class EnemyCrowdCoordinator3D : Node
{
    [Export] public float SevereOverlapDistance { get; set; } = 0.25f;

    private readonly List<EnemyCrowdAgent3D> _agents = new();
    private readonly HashSet<EnemyCrowdAgent3D> _registered = new();
    private int _nextCrowdId = 1;

    public int RegisteredAgentCount => _agents.Count;
    public int PairEvaluationCount { get; private set; }
    public int SevereOverlapPairCount { get; private set; }
    public int UpdateCount { get; private set; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        AddToGroup("enemy_crowd_coordinators_3d");
    }

    public override void _ExitTree()
    {
        for (var i = 0; i < _agents.Count; i++)
        {
            if (GodotObject.IsInstanceValid(_agents[i]))
            {
                _agents[i].ClearCoordinator(this);
            }
        }

        _agents.Clear();
        _registered.Clear();
    }

    public bool Register(EnemyCrowdAgent3D agent)
    {
        if (!GodotObject.IsInstanceValid(agent) || _registered.Contains(agent))
        {
            return false;
        }

        _registered.Add(agent);
        _agents.Add(agent);
        agent.AssignCoordinator(this, _nextCrowdId++);
        return true;
    }

    public bool Unregister(EnemyCrowdAgent3D agent)
    {
        if (!_registered.Remove(agent))
        {
            return false;
        }

        _agents.Remove(agent);
        agent.ClearCoordinator(this);
        return true;
    }

    public EnemyCrowdAgent3D GetRegisteredAgent(int index)
    {
        if (index < 0 || index >= _agents.Count)
        {
            return null;
        }

        return _agents[index];
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateCount++;
        PairEvaluationCount = 0;
        SevereOverlapPairCount = 0;

        for (var i = _agents.Count - 1; i >= 0; i--)
        {
            var agent = _agents[i];
            if (!GodotObject.IsInstanceValid(agent) || !agent.IsActive)
            {
                _registered.Remove(agent);
                _agents.RemoveAt(i);
            }
        }

        for (var i = 0; i < _agents.Count; i++)
        {
            _agents[i].BeginCrowdFrame();
        }

        for (var i = 0; i < _agents.Count; i++)
        {
            var first = _agents[i];
            if (!first.IsActive || !GodotObject.IsInstanceValid(first))
            {
                continue;
            }

            for (var j = i + 1; j < _agents.Count; j++)
            {
                var second = _agents[j];
                if (!second.IsActive || !GodotObject.IsInstanceValid(second))
                {
                    continue;
                }

                PairEvaluationCount++;
                EvaluatePair(first, second);
            }
        }

        for (var i = 0; i < _agents.Count; i++)
        {
            _agents[i].FinishCrowdFrame();
        }
    }

    private void EvaluatePair(EnemyCrowdAgent3D first, EnemyCrowdAgent3D second)
    {
        var offset = first.WorldPosition - second.WorldPosition;
        offset.Y = 0.0f;
        var distanceSquared = offset.LengthSquared();
        var separationDistance = first.BodyRadius + second.BodyRadius
            + Mathf.Max(first.PersonalSpace, second.PersonalSpace);

        if (distanceSquared >= separationDistance * separationDistance)
        {
            return;
        }

        var distance = Mathf.Sqrt(distanceSquared);
        var direction = distance > 0.0001f
            ? offset / distance
            : StableDirection(first.CrowdId, second.CrowdId);
        var penetration = Mathf.Max(0.0f, separationDistance - distance);

        first.ReceivePair(direction * penetration, penetration);
        second.ReceivePair(-direction * penetration, penetration);

        if (distance < SevereOverlapDistance)
        {
            SevereOverlapPairCount++;
        }
    }

    private static Vector3 StableDirection(int firstId, int secondId)
    {
        var hash = unchecked(firstId * 73856093 ^ secondId * 19349663);
        var angle = (hash & 0x7fffffff) % 360 * Mathf.Pi / 180.0f;
        return new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
    }
}
