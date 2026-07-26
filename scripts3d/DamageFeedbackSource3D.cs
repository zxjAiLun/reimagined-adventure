using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// Event-only adapter for presentation feedback. Actors publish the
/// authoritative DamageResult after HealthComponent mitigation; consumers
/// never need to poll an actor's health.
/// </summary>
public partial class DamageFeedbackSource3D : Node3D
{
    public event Action<DamageResult, Vector3> DamageTaken;

    public override void _Ready()
    {
        AddToGroup("damage_feedback_sources_3d");
        CallDeferred(nameof(RegisterWithSpawners));
    }

    public override void _ExitTree()
    {
        if (GetTree() == null)
        {
            return;
        }

        foreach (var node in GetTree().GetNodesInGroup("damage_number_spawners_3d"))
        {
            if (node is DamageNumberSpawner3D spawner)
            {
                spawner.Unregister(this);
            }
        }
    }

    public void Publish(DamageResult result)
    {
        if (!IsInsideTree())
        {
            return;
        }

        DamageTaken?.Invoke(result, GlobalPosition);
    }

    private void RegisterWithSpawners()
    {
        if (!IsInsideTree())
        {
            return;
        }

        foreach (var node in GetTree().GetNodesInGroup("damage_number_spawners_3d"))
        {
            if (node is DamageNumberSpawner3D spawner)
            {
                spawner.Register(this);
            }
        }
    }
}
