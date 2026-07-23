using System;
using Arpg.Domain;
using Godot;

public partial class TestTarget : StaticBody2D, IDamageable
{
    [Export] public int MaxHealth { get; set; } = 100;
    [Export] public float Radius { get; set; } = 30.0f;

    public int CurrentHealth => _health?.CurrentHealth ?? 0;
    public int HitCount { get; private set; }
    public Stats DefensiveStats
    {
        get => _health?.DefensiveStats ?? Stats.Neutral;
        set => _health?.SetDefensiveStats(value);
    }

    public bool IsAlive => _health?.IsAlive ?? false;

    private HealthComponent _health;
    private Label _healthLabel;

    public override void _Ready()
    {
        AddToGroup("damageables");
        _health = GetNode<HealthComponent>("HealthComponent");
        _health.SetMaxHealth(MaxHealth);
        _healthLabel = GetNodeOrNull<Label>("HealthLabel");
        RefreshVisuals();
    }

    public DamageResult ApplyDamage(DamageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = _health.ApplyDamage(request);
        if (result.DamageApplied > 0)
        {
            HitCount++;
        }

        RefreshVisuals();
        return result;
    }

    public void ResetTarget()
    {
        _health.ResetHealth();
        HitCount = 0;
        RefreshVisuals();
    }

    public override void _Draw()
    {
        var healthRatio = MaxHealth > 0 ? (float)CurrentHealth / MaxHealth : 0.0f;
        var fillColor = healthRatio > 0.0f
            ? new Color(0.95f, 0.30f, 0.25f, 1.0f)
            : new Color(0.25f, 0.25f, 0.30f, 1.0f);

        DrawCircle(Vector2.Zero, Radius, fillColor);
        DrawArc(Vector2.Zero, Radius, 0.0f, Mathf.Tau, 32, new Color(1.0f, 0.85f, 0.75f, 1.0f), 3.0f);
        DrawRect(new Rect2(-Radius, Radius + 10.0f, Radius * 2.0f, 6.0f), new Color(0.12f, 0.12f, 0.16f, 1.0f));
        DrawRect(new Rect2(-Radius, Radius + 10.0f, Radius * 2.0f * healthRatio, 6.0f), new Color(0.30f, 0.95f, 0.35f, 1.0f));
    }

    private void RefreshVisuals()
    {
        if (_healthLabel != null)
        {
            _healthLabel.Text = $"TARGET {CurrentHealth}/{MaxHealth}\nHITS {HitCount}";
        }

        QueueRedraw();
    }
}
