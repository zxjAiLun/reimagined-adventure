using System;
using Arpg.Domain;
using Godot;

public partial class SkillAreaEffect3D : Node3D
{
    private SkillDefinition _definition;
    private DamageRequest _damageRequest;
    private float _delayRemaining;
    private float _remainingLifetime;
    private float _radius;
    private bool _impactApplied;
    private MeshInstance3D _visual;

    public override void _Ready()
    {
        _visual = GetNodeOrNull<MeshInstance3D>("Visual");
        UpdateVisualScale();
    }

    public void Configure(
        SkillDefinition definition,
        Vector3 targetPosition,
        DamageRequest damageRequest,
        double radiusOverride)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(damageRequest);
        _definition = definition;
        _damageRequest = damageRequest;
        _radius = (float)radiusOverride;
        _delayRemaining = Mathf.Max(0.0f, (float)definition.CastDelaySeconds);
        _remainingLifetime = Mathf.Max(
            0.18f,
            _delayRemaining + (float)definition.EffectDurationSeconds);
        GlobalPosition = new Vector3(targetPosition.X, 0.04f, targetPosition.Z);
        UpdateVisualScale();
    }

    public override void _Process(double delta)
    {
        if (_definition == null)
        {
            return;
        }

        var frameDelta = (float)delta;
        if (!_impactApplied)
        {
            _delayRemaining -= frameDelta;
            if (_delayRemaining <= 0.0f)
            {
                ApplyImpact();
            }
        }

        _remainingLifetime -= frameDelta;
        if (_remainingLifetime <= 0.0f)
        {
            QueueFree();
        }
    }

    public void ApplyImpactForTest()
    {
        if (!_impactApplied)
        {
            ApplyImpact();
        }
    }

    private void ApplyImpact()
    {
        _impactApplied = true;
        var radiusSquared = _radius * _radius;
        foreach (var node in GetTree().GetNodesInGroup("damageables_3d"))
        {
            if (node is Node3D damageableNode
                && node is ICombatTarget target
                && target.IsAlive
                && CombatTargeting.CanHit(_damageRequest, target)
                && HorizontalDistanceSquared(damageableNode.GlobalPosition, GlobalPosition) <= radiusSquared)
            {
                target.ApplyDamage(_damageRequest);
            }
        }
    }

    private void UpdateVisualScale()
    {
        if (_visual != null && _radius > 0.0f)
        {
            _visual.Scale = new Vector3(_radius, 1.0f, _radius);
        }
    }

    private static float HorizontalDistanceSquared(Vector3 first, Vector3 second)
    {
        var delta = first - second;
        delta.Y = 0.0f;
        return delta.LengthSquared();
    }
}
