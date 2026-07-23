using System;
using System.Collections.Generic;
using Arpg.Domain;
using Godot;

/// <summary>
/// Minimal vertical-slice inventory adapter. Inventory owns item instances;
/// Equipment owns the active item per slot, and the player receives the
/// resulting aggregate stats.
/// </summary>
public partial class InventoryController : Node
{
    [Signal]
    public delegate void InventoryChangedEventHandler();

    [Signal]
    public delegate void EquipmentChangedEventHandler();

    [Export] public int Capacity { get; set; } = 8;
    [Export] public float PickupRange { get; set; } = 84.0f;
    [Export] public int PlayerLevel { get; set; } = 1;

    private readonly List<Item> _items = new();
    private PlayerController _player;

    public Equipment Equipment { get; } = new();
    public IReadOnlyList<Item> Items => _items;
    public int ItemCount => _items.Count;
    public Item? EquippedWeapon => Equipment.ItemInSlot(EquipmentSlot.Weapon);
    public int SpreadShotDamage => CombatMath.SkillDamage(
        SkillLibrary.SpreadShot().BaseDamage,
        _player?.EffectiveStats ?? Stats.Neutral,
        DamageType.Physical,
        SkillDamageCategory.Projectile);

    public override void _Ready()
    {
        Capacity = Mathf.Max(1, Capacity);
        PickupRange = Mathf.Max(1.0f, PickupRange);
        PlayerLevel = Mathf.Max(1, PlayerLevel);
        _player = GetParent<PlayerController>();
        RecalculatePlayerStats();
        SetProcessUnhandledInput(true);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("pickup_item", true))
        {
            TryPickupNearest();
        }
        else if (@event.IsActionPressed("equip_item", true))
        {
            TryEquipNewestWeapon();
        }
    }

    public bool TryAddItem(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Validate();
        if (_items.Count >= Capacity)
        {
            return false;
        }

        _items.Add(item);
        EmitSignal(SignalName.InventoryChanged);
        return true;
    }

    public bool TryEquipItem(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            return false;
        }

        var item = _items[index];
        if (!Equipment.CanEquip(item, PlayerLevel))
        {
            return false;
        }

        var replaced = Equipment.Equip(item, PlayerLevel);
        if (replaced == null)
        {
            _items.RemoveAt(index);
        }
        else
        {
            // Keep the replaced weapon in the same inventory slot. This makes
            // replacement atomic and guarantees the old item is not lost.
            _items[index] = replaced;
        }

        RecalculatePlayerStats();
        EmitSignal(SignalName.InventoryChanged);
        EmitSignal(SignalName.EquipmentChanged);
        return true;
    }

    public bool TryEquipNewestWeapon()
    {
        for (var index = _items.Count - 1; index >= 0; index--)
        {
            if (_items[index].Slot == EquipmentSlot.Weapon)
            {
                return TryEquipItem(index);
            }
        }

        return false;
    }

    public bool TryRestoreSavedState(MinimalRunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.InventoryItems == null || state.InventoryItems.Count > Capacity)
        {
            return false;
        }

        var restoredItems = new List<Item>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in state.InventoryItems)
        {
            if (item == null || !ids.Add(item.Id))
            {
                return false;
            }

            try
            {
                item.Validate();
            }
            catch (ArgumentException)
            {
                return false;
            }

            restoredItems.Add(item);
        }

        if (state.EquippedWeapon != null)
        {
            if (ids.Contains(state.EquippedWeapon.Id)
                || !Equipment.CanEquip(state.EquippedWeapon, PlayerLevel))
            {
                return false;
            }
        }

        _items.Clear();
        _items.AddRange(restoredItems);
        Equipment.Reset();
        if (state.EquippedWeapon != null)
        {
            Equipment.Equip(state.EquippedWeapon, PlayerLevel);
        }

        RecalculatePlayerStats();
        EmitSignal(SignalName.InventoryChanged);
        EmitSignal(SignalName.EquipmentChanged);
        return true;
    }

    public bool TryRemoveItem(int index, out Item item)
    {
        item = null;
        if (index < 0 || index >= _items.Count)
        {
            return false;
        }

        item = _items[index];
        _items.RemoveAt(index);
        EmitSignal(SignalName.InventoryChanged);
        return true;
    }

    public bool TryReplaceItem(int index, Item replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        replacement.Validate();
        if (index < 0 || index >= _items.Count)
        {
            return false;
        }

        _items[index] = replacement;
        EmitSignal(SignalName.InventoryChanged);
        return true;
    }

    public bool TryPickupNearest(float? rangeOverride = null)
    {
        if (_player == null || _items.Count >= Capacity)
        {
            return false;
        }

        var range = rangeOverride ?? PickupRange * _player.EffectiveStats.PickupRangeMultiplier;
        ItemDrop nearest = null;
        var nearestDistanceSquared = range * range;
        foreach (var node in GetTree().GetNodesInGroup("item_drops"))
        {
            if (node is not ItemDrop drop)
            {
                continue;
            }

            var distanceSquared = drop.GlobalPosition.DistanceSquaredTo(_player.GlobalPosition);
            if (distanceSquared <= nearestDistanceSquared)
            {
                nearest = drop;
                nearestDistanceSquared = distanceSquared;
            }
        }

        return nearest != null && nearest.TryCollect(_player);
    }

    public string InventorySummary()
    {
        var equippedName = EquippedWeapon?.Name ?? "none";
        return $"Bag {_items.Count}/{Capacity} | Weapon: {equippedName} | Spread damage: {SpreadShotDamage}";
    }

    private void RecalculatePlayerStats()
    {
        _player?.SetEquipmentStats(Equipment.CombinedStats());
    }

}
