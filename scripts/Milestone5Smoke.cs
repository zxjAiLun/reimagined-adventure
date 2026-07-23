using System.Collections.Generic;
using Arpg.Domain;
using Godot;

/// <summary>
/// Runtime smoke for the first equipment loop: drop, F-style nearest pickup,
/// inventory retention, replacement and effective damage recalculation.
/// </summary>
public partial class Milestone5Smoke : Node2D
{
    private readonly List<string> _errors = new();
    private PlayerController _player;
    private Item _firstItem;
    private Item _secondItem;
    private int _damageBefore;

    public override void _Ready()
    {
        _player = GetNode<PlayerController>("Player");
        var generator = new LootGenerator(0x5A17UL);
        _firstItem = generator.GenerateWeaponDrop(1);
        _secondItem = generator.GenerateWeaponDrop(1);
        SpawnDrop(_firstItem, new Vector2(12.0f, 0.0f));
        SpawnDrop(_secondItem, new Vector2(30.0f, 0.0f));

        if (_player.Inventory == null)
        {
            _errors.Add("Player did not initialize InventoryController");
            Finish();
            return;
        }

        _damageBefore = _player.Inventory.SpreadShotDamage;
        if (!_player.Inventory.TryPickupNearest(84.0f))
        {
            _errors.Add("nearest item could not be picked up");
        }

        GetTree().CreateTimer(0.10).Timeout += CheckFirstPickup;
    }

    private void CheckFirstPickup()
    {
        if (_player.Inventory.ItemCount != 1)
        {
            _errors.Add($"first pickup produced inventory count {_player.Inventory.ItemCount}, expected 1");
        }

        if (!_player.Inventory.TryPickupNearest(84.0f))
        {
            _errors.Add("second item could not be picked up");
        }

        GetTree().CreateTimer(0.10).Timeout += CheckEquipment;
    }

    private void CheckEquipment()
    {
        if (_player.Inventory.ItemCount != 2)
        {
            _errors.Add($"two pickups produced inventory count {_player.Inventory.ItemCount}, expected 2");
            Finish();
            return;
        }

        if (!_player.Inventory.TryEquipItem(0))
        {
            _errors.Add("first weapon could not be equipped");
            Finish();
            return;
        }

        var damageAfterFirst = _player.Inventory.SpreadShotDamage;
        if (damageAfterFirst <= _damageBefore)
        {
            _errors.Add($"first weapon did not change damage: {_damageBefore} -> {damageAfterFirst}");
        }

        var oldWeapon = _player.Inventory.EquippedWeapon;
        if (!_player.Inventory.TryEquipNewestWeapon())
        {
            _errors.Add("second weapon could not be equipped");
            Finish();
            return;
        }

        var damageAfterReplacement = _player.Inventory.SpreadShotDamage;
        if (damageAfterReplacement <= _damageBefore)
        {
            _errors.Add($"replacement weapon did not leave effective damage above neutral: {_damageBefore} -> {damageAfterReplacement}");
        }

        if (_player.Inventory.ItemCount != 1
            || _player.Inventory.Items[0] != oldWeapon)
        {
            _errors.Add("replaced weapon did not return to inventory");
        }

        Finish();
    }

    private void SpawnDrop(Item item, Vector2 offset)
    {
        var scene = GD.Load<PackedScene>("res://scenes/ItemDrop.tscn");
        var drop = scene.Instantiate<ItemDrop>();
        AddChild(drop);
        drop.GlobalPosition = _player.GlobalPosition + offset;
        drop.Configure(item);
    }

    private void Finish()
    {
        if (_errors.Count == 0)
        {
            GD.Print($"MILESTONE5_SMOKE_PASS damage={_damageBefore}->{_player.Inventory.SpreadShotDamage}");
            GetTree().Quit(0);
            return;
        }

        foreach (var error in _errors)
        {
            GD.PrintErr($"MILESTONE5_SMOKE_FAIL {error}");
        }

        GetTree().Quit(1);
    }
}
