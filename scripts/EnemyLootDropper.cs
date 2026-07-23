using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// Bridges a HealthComponent death signal to one deterministic weapon drop.
/// It is intentionally a small child node so enemy AI remains independent of
/// item generation.
/// </summary>
public partial class EnemyLootDropper : Node
{
    [Export] public PackedScene ItemDropScene { get; set; }
    [Export] public int ItemLevel { get; set; } = 1;
    [Export] public int DropSeed { get; set; } = 1;
    [Export] public bool BossDrop { get; set; }

    private HealthComponent _health;
    private Node2D _owner;
    private RunSessionNode _runSession;
    private bool _dropped;

    public override void _Ready()
    {
        AddToGroup("enemy_loot_droppers");
        _owner = GetParent<Node2D>();
        _runSession = GetTree().GetFirstNodeInGroup("run_sessions") as RunSessionNode;
        _health = _owner.GetNodeOrNull<HealthComponent>("HealthComponent");
        if (_health != null)
        {
            _health.Died += OnOwnerDied;
        }
    }

    public void ApplyMapModifier(MapModifierNode modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        ItemLevel = modifier.ScaleItemLevel(ItemLevel);
    }

    private void OnOwnerDied()
    {
        if (_dropped || ItemDropScene == null || _owner == null || !_owner.IsInsideTree())
        {
            return;
        }

        _dropped = true;
        var item = _runSession != null
            ? _runSession.GenerateWeaponDrop(Mathf.Max(1, ItemLevel), BossDrop)
            : new LootGenerator((ulong)Mathf.Max(1, DropSeed))
                .GenerateWeaponDrop(Mathf.Max(1, ItemLevel), BossDrop);
        var drop = ItemDropScene.Instantiate<ItemDrop>();
        _owner.GetParent().AddChild(drop);
        drop.GlobalPosition = _owner.GlobalPosition;
        drop.Configure(item);
    }
}
