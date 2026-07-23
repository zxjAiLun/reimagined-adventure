using Godot;

public partial class HitEffect : Node2D
{
    [Export] public float Lifetime { get; set; } = 0.28f;

    public override void _Ready()
    {
        GetTree().CreateTimer(Mathf.Max(0.05f, Lifetime)).Timeout += QueueFree;
    }
}
