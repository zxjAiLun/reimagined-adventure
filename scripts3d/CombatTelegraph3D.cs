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
        Progress = 0.0f;
        IsActive = true;
        Visible = true;
        RefreshVisual();
    }

    public void Cancel()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        Visible = false;
        QueueFree();
    }

    /// <summary>
    /// The owning attack state machine supplies progress from its physics
    /// clock. The telegraph never advances or ends itself.
    /// </summary>
    public void SetProgress(float progress)
    {
        if (!IsActive)
        {
            return;
        }

        Progress = Mathf.Clamp(progress, 0.0f, 1.0f);
        RefreshVisual();
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

    protected virtual void RefreshVisual()
    {
    }
}
