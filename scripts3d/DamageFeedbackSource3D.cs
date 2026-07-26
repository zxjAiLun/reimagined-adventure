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
    }

    public void Publish(DamageResult result)
    {
        if (!IsInsideTree())
        {
            return;
        }

        DamageTaken?.Invoke(result, GlobalPosition);
    }
}
