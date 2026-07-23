using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// A delayed enemy telegraph that only resolves against the player group.
/// </summary>
public partial class EnemyAreaEffect : Node2D
{
    private DamageRequest _damageRequest = new(0, DamageType.Physical, "unconfigured");
    private float _radius;
    private float _delayRemaining;
    private float _visualRemaining;
    private float _visualDuration;
    private Color _effectColor = new(1.0f, 0.25f, 0.05f, 1.0f);
    private bool _impactApplied;

    public void Configure(
        Vector2 targetPosition,
        float radius,
        float delay,
        float visualDuration,
        DamageRequest damageRequest,
        Color effectColor)
    {
        if (radius <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Area radius must be positive.");
        }

        _radius = radius;
        _delayRemaining = Mathf.Max(0.0f, delay);
        _visualDuration = Mathf.Max(0.18f, visualDuration);
        _visualRemaining = _delayRemaining + _visualDuration;
        _damageRequest = damageRequest ?? throw new ArgumentNullException(nameof(damageRequest));
        _effectColor = effectColor;
        GlobalPosition = targetPosition;
    }

    public override void _Ready()
    {
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
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
        var telegraph = !_impactApplied;
        var alpha = telegraph ? 0.22f : 0.42f;
        var fill = new Color(_effectColor.R, _effectColor.G, _effectColor.B, alpha);
        var progress = _visualDuration > 0.0f
            ? Mathf.Clamp(_visualRemaining / _visualDuration, 0.0f, 1.0f)
            : 1.0f;

        DrawCircle(Vector2.Zero, _radius, fill);
        DrawArc(
            Vector2.Zero,
            _radius,
            0.0f,
            Mathf.Tau * Mathf.Max(0.05f, progress),
            48,
            new Color(_effectColor.R, _effectColor.G, _effectColor.B, 0.95f),
            4.0f);
    }

    private void ApplyImpact()
    {
        _impactApplied = true;
        var player = GetTree().GetFirstNodeInGroup("player");
        if (player is Node2D playerNode
            && player is IDamageable target
            && target.IsAlive
            && playerNode.GlobalPosition.DistanceSquaredTo(GlobalPosition) <= _radius * _radius)
        {
            target.ApplyDamage(_damageRequest);
        }
    }
}
