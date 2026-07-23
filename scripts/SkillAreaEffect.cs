using System;
using Arpg.Domain;
using Godot;

public partial class SkillAreaEffect : Node2D
{
    [Export] public PackedScene HitEffectScene { get; set; }
    private SkillDefinition _definition;
    private Vector2 _targetPosition;
    private DamageRequest _damageRequest;
    private float _delayRemaining;
    private float _visualRemaining;
    private float _visualDuration;
    private bool _impactApplied;

    public void Configure(SkillDefinition definition, Vector2 targetPosition, DamageRequest damageRequest)
    {
        _definition = definition;
        _targetPosition = targetPosition;
        _damageRequest = damageRequest ?? throw new ArgumentNullException(nameof(damageRequest));
        _delayRemaining = (float)definition.CastDelaySeconds;
        _visualDuration = Mathf.Max(0.18f, (float)definition.EffectDurationSeconds);
        _visualRemaining = _delayRemaining + _visualDuration;
        GlobalPosition = targetPosition;
    }

    public override void _Ready()
    {
        if (_definition == null)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
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

        _visualRemaining -= frameDelta;
        QueueRedraw();
        if (_visualRemaining <= 0.0f)
        {
            QueueFree();
        }
    }

    public override void _Draw()
    {
        if (_definition == null)
        {
            return;
        }

        var radius = (float)_definition.Radius;
        var progress = _visualDuration > 0.0f
            ? Mathf.Clamp(_visualRemaining / _visualDuration, 0.0f, 1.0f)
            : 1.0f;
        var telegraph = !_impactApplied;
        var color = _definition.DamageType switch
        {
            DamageType.Fire => new Color(1.0f, 0.34f, 0.10f, telegraph ? 0.24f : 0.38f),
            DamageType.Lightning => new Color(0.30f, 0.68f, 1.0f, telegraph ? 0.22f : 0.36f),
            _ => new Color(0.90f, 0.90f, 0.95f, telegraph ? 0.20f : 0.32f),
        };

        DrawCircle(Vector2.Zero, radius, color);
        var outlineColor = new Color(color.R, color.G, color.B, 0.95f);
        DrawArc(
            Vector2.Zero,
            radius,
            0.0f,
            Mathf.Tau * Mathf.Max(0.05f, progress),
            48,
            outlineColor,
            4.0f);

        if (_definition.Id == "meteor")
        {
            DrawCircle(Vector2.Zero, Mathf.Min(18.0f, radius * 0.22f), new Color(1.0f, 0.82f, 0.20f, 0.95f));
        }
    }

    private void ApplyImpact()
    {
        _impactApplied = true;
        var radiusSquared = (float)(_definition.Radius * _definition.Radius);
        foreach (var node in GetTree().GetNodesInGroup("damageables"))
        {
            if (node is Node2D damageableNode
                && node is IDamageable target
                && target.IsAlive
                && damageableNode.GlobalPosition.DistanceSquaredTo(_targetPosition) <= radiusSquared)
            {
                target.ApplyDamage(_damageRequest);
            }
        }

        if (HitEffectScene != null)
        {
            var effect = HitEffectScene.Instantiate<HitEffect>();
            GetParent().AddChild(effect);
            effect.GlobalPosition = _targetPosition;
        }
    }
}
