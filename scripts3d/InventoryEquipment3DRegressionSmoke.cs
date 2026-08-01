using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// Stage 10 smoke for the real multi-slot Player/Build adapter. It uses direct
/// build methods so UI coordinates never become business-rule coverage.
/// </summary>
public partial class InventoryEquipment3DRegressionSmoke : Node
{
    private PlayerController3D _player;
    private PlayerBuildController3D _build;
    private double _elapsed;
    private bool _complete;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _player = GetNode<PlayerController3D>("Player3D");
        _build = _player.GetNode<PlayerBuildController3D>("PlayerBuildController3D");
    }

    public override void _Process(double delta)
    {
        if (_complete)
        {
            return;
        }

        _elapsed += delta;
        if (_elapsed < 0.25)
        {
            return;
        }

        try
        {
            RunSmoke();
            _complete = true;
            GD.Print("INVENTORY_EQUIPMENT_3D_SPIKE_PASS four_slots=true stats=true hp_preserved=true ui=true");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            _complete = true;
            GD.PushError($"INVENTORY_EQUIPMENT_3D_SPIKE_FAIL {exception.Message}");
            GetTree().Quit(1);
        }
    }

    private void RunSmoke()
    {
        var generator = new LootGenerator(0x10_10_10UL);
        var items = new[]
        {
            generator.GenerateItemDrop(new ItemRollContext(1, LootSourceKind.Reward, EquipmentSlot.Weapon, ForcedRarity: Rarity.Magic)),
            generator.GenerateItemDrop(new ItemRollContext(1, LootSourceKind.Reward, EquipmentSlot.Armor, ForcedRarity: Rarity.Magic)),
            generator.GenerateItemDrop(new ItemRollContext(1, LootSourceKind.Reward, EquipmentSlot.Ring, ForcedRarity: Rarity.Magic)),
            generator.GenerateItemDrop(new ItemRollContext(1, LootSourceKind.Reward, EquipmentSlot.Amulet, ForcedRarity: Rarity.Magic)),
        };

        foreach (var item in items)
        {
            if (!_player.TryAddItem(item))
            {
                throw new InvalidOperationException($"could not add {item.Slot} item");
            }
        }

        var maxHealthBefore = _player.MaxHealth;
        var currentHealthBefore = _player.CurrentHealth;
        foreach (var item in items)
        {
            if (!_player.TryEquipItem(item.Id))
            {
                throw new InvalidOperationException($"could not equip {item.Slot} item");
            }
        }

        if (_player.EquippedItems.Count != 4
            || _player.Equipment.ItemInSlot(EquipmentSlot.Weapon) == null
            || _player.Equipment.ItemInSlot(EquipmentSlot.Armor) == null
            || _player.Equipment.ItemInSlot(EquipmentSlot.Ring) == null
            || _player.Equipment.ItemInSlot(EquipmentSlot.Amulet) == null)
        {
            throw new InvalidOperationException("all four equipment slots were not populated");
        }

        if (_player.MaxHealth <= maxHealthBefore || _player.CurrentHealth != currentHealthBefore)
        {
            throw new InvalidOperationException("equipment changed max health with an unintended heal");
        }

        if (_player.EffectiveStats.ProjectileDamageMultiplier <= 1.0
            || _player.EffectiveStats.AreaDamageMultiplier <= 1.0)
        {
            throw new InvalidOperationException("multi-slot stats did not reach effective player stats");
        }

        if (!_build.Open())
        {
            throw new InvalidOperationException("build screen did not open");
        }

        var screen = _build.Screen;
        if (screen == null
            || !screen.IsScreenVisible
            || !screen.ItemsText.Contains("Inventory", StringComparison.Ordinal)
            || !screen.EquipmentText.Contains("Weapon", StringComparison.Ordinal)
            || !screen.DetailsText.Contains("Effective Stats", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("inventory UI did not present build data");
        }

        if (!_build.Close() || GetTree().Paused)
        {
            throw new InvalidOperationException("build screen did not close and resume the map");
        }
    }
}
