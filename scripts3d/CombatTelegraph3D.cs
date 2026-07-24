using Godot;

/// <summary>
/// Reusable, presentation-only warning node for 3D combat attacks.
/// It is pausable with the gameplay tree and never applies damage itself.
/// </summary>
public partial class CombatTelegraph3D : Node3D
{
    public float Progress { get; private set; }
    public bool IsActive { get; private set; }
    public float Duration { get; private set; }
    public Vector3 TelegraphWorldPosition => GlobalPosition;

    private float _elapsed;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        AddToGroup("combat_telegraphs_3d");
        Visible = false;
    }

    public void Activate(Vector3 worldPosition, float duration)
    {
        GlobalPosition = worldPosition;
        Duration = Mathf.Max(0.01f, duration);
        _elapsed = 0.0f;
        Progress = 0.0f;
        IsActive = true;
        Visible = true;
        RefreshVisual();
    }

    public void Cancel()
    {
        if (!IsActive && !GodotObject.IsInstanceValid(this))
        {
            return;
        }

        IsActive = false;
        Visible = false;
        QueueFree();
    }

    public void Complete()
    {
        if (!IsActive)
        {
            return;
        }

        Progress = 1.0f;
        IsActive = false;
        Visible = false;
        QueueFree();
    }

    public override void _Process(double delta)
    {
        if (!IsActive)
        {
            return;
        }

        _elapsed += (float)delta;
        Progress = Mathf.Clamp(_elapsed / Duration, 0.0f, 1.0f);
        RefreshVisual();
        if (_elapsed >= Duration)
        {
            Complete();
        }
    }

    protected virtual void RefreshVisual()
    {
    }
}
