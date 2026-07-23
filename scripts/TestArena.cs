using Godot;

public partial class TestArena : Node2D
{
    [Export] public Vector2 ArenaSize { get; set; } = new(1600.0f, 900.0f);
    [Export] public int GridSpacing { get; set; } = 80;
    [Export] public int MapLevel { get; set; } = 1;

    public override void _Ready()
    {
        var runSession = GetTree().GetFirstNodeInGroup("run_sessions") as RunSessionNode;
        var mapModifier = GetNodeOrNull<MapModifierNode>("MapModifier");
        mapModifier?.ConfigureMapLevel(runSession?.CurrentMapLevel ?? MapLevel);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var bounds = new Rect2(Vector2.Zero, ArenaSize);
        DrawRect(bounds, new Color(0.035f, 0.055f, 0.08f, 1.0f));

        for (var x = 0.0f; x <= ArenaSize.X; x += GridSpacing)
        {
            DrawLine(new Vector2(x, 0.0f), new Vector2(x, ArenaSize.Y), new Color(0.10f, 0.16f, 0.22f, 1.0f), 1.0f);
        }

        for (var y = 0.0f; y <= ArenaSize.Y; y += GridSpacing)
        {
            DrawLine(new Vector2(0.0f, y), new Vector2(ArenaSize.X, y), new Color(0.10f, 0.16f, 0.22f, 1.0f), 1.0f);
        }

        DrawRect(bounds, new Color(0.25f, 0.65f, 0.85f, 1.0f), false, 4.0f);
    }
}
