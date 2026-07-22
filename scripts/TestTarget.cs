using Godot;

public partial class TestTarget : StaticBody2D
{
    [Export] public int MaxHealth { get; set; } = 100;
    [Export] public float Radius { get; set; } = 30.0f;

    public int CurrentHealth { get; private set; }
    public int HitCount { get; private set; }

    private Label _healthLabel;

    public override void _Ready()
    {
        AddToGroup("skill_targets");
        CurrentHealth = Mathf.Max(1, MaxHealth);
        _healthLabel = GetNodeOrNull<Label>("HealthLabel");
        RefreshVisuals();
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || CurrentHealth <= 0)
        {
            return;
        }

        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        HitCount++;
        RefreshVisuals();
    }

    public void ResetTarget()
    {
        CurrentHealth = Mathf.Max(1, MaxHealth);
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
