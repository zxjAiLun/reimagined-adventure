using Arpg.Domain;
using Godot;

/// <summary>
/// Godot adapter for simple one-shot map events. Complex encounter spawning
/// and map generation are intentionally outside this node.
/// </summary>
public partial class MapEventNode : Node2D
{
    [Export] public MapEventResource DefinitionResource { get; set; }
    [Export] public PackedScene ItemDropScene { get; set; }
    [Export] public int DropSeed { get; set; } = 1201;

    private MapEventDefinition _definition;
    private readonly MapEventState _state = new();
    private Label _label;

    public bool IsCompleted => _state.Completed;
    public MapEventType EventType => _definition?.Type ?? MapEventType.LootCache;

    public override void _Ready()
    {
        AddToGroup("map_events");
        _definition = DefinitionResource?.ToDomain() ?? MapEventLibrary.LootCache();
        _label = GetNodeOrNull<Label>("Label");
        RefreshLabel();
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("pickup_item", true))
        {
            TryActivate(GetTree().GetFirstNodeInGroup("player") as PlayerController);
        }
    }

    public bool TryActivate(PlayerController player)
    {
        if (player == null
            || !player.IsAlive
            || !_state.CanActivate
            || GlobalPosition.DistanceTo(player.GlobalPosition) > _definition.Radius)
        {
            return false;
        }

        if (!_state.TryActivate(_definition, out var activation))
        {
            return false;
        }

        if (activation!.Type == MapEventType.LootCache)
        {
            SpawnCacheDrops(player, activation.ItemDropCount);
            _label.Text = $"{_definition.Name}\nOpened: {activation.ItemDropCount} drops";
        }
        else
        {
            _label.Text = $"{_definition.Name}\n+{(int)((activation.DamageMultiplier - 1.0) * 100.0)}% damage for {activation.BuffDurationSeconds:0}s";
        }

        QueueRedraw();
        return true;
    }

    public override void _Draw()
    {
        var color = EventType == MapEventType.Shrine
            ? new Color(0.36f, 0.72f, 1.0f, 1.0f)
            : new Color(1.0f, 0.72f, 0.24f, 1.0f);
        DrawCircle(Vector2.Zero, (float)(_definition?.Radius ?? 70.0), new Color(color, 0.10f));
        DrawArc(Vector2.Zero, 18.0f, 0.0f, Mathf.Tau, 24, color, 3.0f);
        DrawLine(new Vector2(-9.0f, 0.0f), new Vector2(9.0f, 0.0f), color, 3.0f);
        DrawLine(new Vector2(0.0f, -9.0f), new Vector2(0.0f, 9.0f), color, 3.0f);
    }

    private void SpawnCacheDrops(PlayerController player, int count)
    {
        if (ItemDropScene == null)
        {
            return;
        }

        var generator = new LootGenerator((ulong)Mathf.Max(1, DropSeed));
        for (var index = 0; index < count; index++)
        {
            var drop = ItemDropScene.Instantiate<ItemDrop>();
            GetParent().AddChild(drop);
            var offset = new Vector2(index * 30.0f - (count - 1) * 15.0f, 28.0f);
            drop.GlobalPosition = player.GlobalPosition + offset;
            drop.Configure(generator.GenerateWeaponDrop(1));
        }
    }

    private void RefreshLabel()
    {
        if (_label != null)
        {
            _label.Text = _definition?.Name ?? "Map Event";
        }
    }
}
