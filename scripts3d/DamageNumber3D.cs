using System.Globalization;
using Godot;

/// <summary>
/// Presentation-only world-space damage number. It receives a final integer
/// and never participates in combat calculation or target lookup.
/// </summary>
public partial class DamageNumber3D : Label3D
{
    [Export] public float LifetimeSeconds { get; set; } = 0.75f;
    [Export] public float RiseSpeed { get; set; } = 1.4f;

    public int DamageAmount { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public bool IsComplete { get; private set; }

    private Color _baseColor = new(1.0f, 0.82f, 0.22f, 1.0f);

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        Visible = false;
    }

    public void Configure(int damageApplied, Vector3 worldPosition)
    {
        if (damageApplied <= 0)
        {
            IsComplete = true;
            QueueFree();
            return;
        }

        DamageAmount = damageApplied;
        Text = damageApplied.ToString(CultureInfo.InvariantCulture);
        GlobalPosition = worldPosition;
        ElapsedSeconds = 0.0f;
        IsComplete = false;
        Visible = true;
        Modulate = _baseColor;
    }

    public override void _Process(double delta)
    {
        if (IsComplete || !Visible)
        {
            return;
        }

        var frameDelta = Mathf.Max(0.0f, (float)delta);
        ElapsedSeconds += frameDelta;
        GlobalPosition += Vector3.Up * RiseSpeed * frameDelta;

        var lifetime = Mathf.Max(0.01f, LifetimeSeconds);
        var progress = Mathf.Clamp(ElapsedSeconds / lifetime, 0.0f, 1.0f);
        var color = _baseColor;
        color.A = 1.0f - progress;
        Modulate = color;
        if (progress >= 1.0f)
        {
            IsComplete = true;
            QueueFree();
        }
    }
}
