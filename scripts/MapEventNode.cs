using Arpg.Domain;
using Godot;

/// <summary>
/// Godot adapter for simple one-shot map events. Complex encounter spawning
/// and map generation are intentionally outside this node.
/// </summary>
public partial class MapEventNode : Node2D, IPlayerInteractable
{
    [Export] public MapEventResource DefinitionResource { get; set; }
    [Export] public PackedScene ItemDropScene { get; set; }
    [Export] public int DropSeed { get; set; } = 1201;

    private MapEventDefinition _definition;
    private readonly MapEventState _state = new();
    private Label _label;
    private RunSessionNode _runSession;
    public bool HasActiveShrineBuff { get; private set; }

    public bool IsCompleted => _state.Completed;
    public MapEventType EventType => _definition?.Type ?? MapEventType.LootCache;
    public int InteractionPriority => 100;

    public override void _Ready()
    {
        AddToGroup("map_events");
        AddToGroup("interactables");
        SetProcessUnhandledInput(false);
        _runSession = GetTree().GetFirstNodeInGroup("run_sessions") as RunSessionNode;
        _definition = DefinitionResource?.ToDomain() ?? MapEventLibrary.LootCache();
        _label = GetNodeOrNull<Label>("Label");
        RefreshLabel();
        QueueRedraw();
    }

    public bool CanInteract(PlayerController player)
    {
        return player != null
            && player.IsAlive
            && _state.CanActivate
            && GlobalPosition.DistanceTo(player.GlobalPosition) <= (_definition?.Radius ?? 0.0);
    }

    public bool TryInteract(PlayerController player) => TryActivate(player);

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
            var mapModifier = GetTree().GetFirstNodeInGroup("map_modifiers") as MapModifierNode;
            var itemQuantityMultiplier = mapModifier?.ItemQuantityMultiplier ?? 1.0;
            var eventRewardMultiplier = mapModifier?.EventRewardMultiplier ?? 1.0;
            var dropCount = Mathf.Max(
                1,
                Mathf.CeilToInt((float)(activation.ItemDropCount
                    * activation.RewardMultiplier
                    * player.EffectiveStats.ItemQuantityMultiplier
                    * eventRewardMultiplier
                    * itemQuantityMultiplier)));
            SpawnCacheDrops(player, dropCount);
            _label.Text = $"{_definition.Name}\nOpened: {dropCount} drops";
        }
        else
        {
            HasActiveShrineBuff = true;
            player.SetEventStats(new Stats
            {
                DamageMultiplier = activation.DamageMultiplier,
            });
            GetTree().CreateTimer((float)activation.BuffDurationSeconds, processAlways: false).Timeout += ClearShrineBuff;
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
        var itemDropScene = ItemDropScene ?? GD.Load<PackedScene>("res://scenes/ItemDrop.tscn");
        if (itemDropScene == null)
        {
            return;
        }

        for (var index = 0; index < count; index++)
        {
            var drop = itemDropScene.Instantiate<ItemDrop>();
            GetParent().AddChild(drop);
            var offset = new Vector2(index * 30.0f - (count - 1) * 15.0f, 28.0f);
            drop.GlobalPosition = player.GlobalPosition + offset;
            var item = _runSession != null
                ? _runSession.GenerateWeaponDrop(Mathf.Max(1, _runSession.CurrentMapLevel))
                : new LootGenerator((ulong)Mathf.Max(1, DropSeed)).GenerateWeaponDrop(1);
            drop.Configure(item);
        }
    }

    private void RefreshLabel()
    {
        if (_label != null)
        {
            _label.Text = _definition?.Name ?? "Map Event";
        }
    }

    private void ClearShrineBuff()
    {
        if (!HasActiveShrineBuff)
        {
            return;
        }

        HasActiveShrineBuff = false;
        var player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
        player?.SetEventStats(Stats.Neutral);
    }
}
