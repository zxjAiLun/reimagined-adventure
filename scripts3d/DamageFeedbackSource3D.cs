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
        var spawner = FindMapSpawner();
        if (spawner != null)
        {
            spawner.Unregister(this);
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

        var spawner = FindMapSpawner();
        if (spawner != null)
        {
            spawner.Register(this);
        }
    }

    private DamageNumberSpawner3D FindMapSpawner()
    {
        for (var current = GetParent(); current != null; current = current.GetParent())
        {
            var spawner = current.GetNodeOrNull<DamageNumberSpawner3D>("DamageNumberSpawner3D");
            if (spawner != null)
            {
                return spawner;
            }
        }

        return null;
    }
}
