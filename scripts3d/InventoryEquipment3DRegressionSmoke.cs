using System;
using System.Linq;
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

        var ironhide = generator.GenerateItemDrop(new ItemRollContext(
            1,
            LootSourceKind.Reward,
            EquipmentSlot.Armor,
            ForcedRarity: Rarity.Unique,
            ForcedBaseId: "ironhide_vest"));
        if (!_player.TryAddItem(ironhide)
            || !_player.TryEquipItem(ironhide.Id)
            || !_player.Items.Any(item => item.Id == items[1].Id))
        {
            throw new InvalidOperationException("armor replacement did not return the old armor to inventory");
        }

        var initialArmoredHit = _player.ApplyDamage(new DamageRequest(
            20,
            DamageType.Physical,
            "inventory_equipment_no_armor",
            CombatFaction.Enemy));
        _player.ApplyRestoredHealth(_player.MaxHealth);
        if (!_player.TryUnequip(EquipmentSlot.Armor))
        {
            throw new InvalidOperationException("could not unequip armor for mitigation baseline");
        }

        var baselinePhysical = _player.ApplyDamage(new DamageRequest(
            20,
            DamageType.Physical,
            "inventory_equipment_baseline",
            CombatFaction.Enemy));
        _player.ApplyRestoredHealth(_player.MaxHealth);
        if (!_player.TryEquipItem(ironhide.Id))
        {
            throw new InvalidOperationException("could not re-equip Ironhide Vest");
        }

        var armoredPhysical = _player.ApplyDamage(new DamageRequest(
            20,
            DamageType.Physical,
            "inventory_equipment_armor",
            CombatFaction.Enemy));
        _player.ApplyRestoredHealth(_player.MaxHealth);
        if (baselinePhysical.DamageApplied <= armoredPhysical.DamageApplied
            || initialArmoredHit.DamageApplied <= 0)
        {
            throw new InvalidOperationException(
                $"Armor did not change actual DamageResult baseline={baselinePhysical.DamageApplied} armored={armoredPhysical.DamageApplied}");
        }

        var emberweave = generator.GenerateItemDrop(new ItemRollContext(
            1,
            LootSourceKind.Reward,
            EquipmentSlot.Armor,
            ForcedRarity: Rarity.Unique,
            ForcedBaseId: "emberweave_coat"));
        if (!_player.TryAddItem(emberweave)
            || !_player.TryEquipItem(emberweave.Id))
        {
            throw new InvalidOperationException("could not equip Emberweave Coat");
        }

        if (!_player.TryUnequip(EquipmentSlot.Armor))
        {
            throw new InvalidOperationException("could not unequip Emberweave Coat for resistance baseline");
        }

        var baselineFire = _player.ApplyDamage(new DamageRequest(
            20,
            DamageType.Fire,
            "inventory_equipment_fire_baseline",
            CombatFaction.Enemy));
        _player.ApplyRestoredHealth(_player.MaxHealth);
        if (!_player.TryEquipItem(emberweave.Id))
        {
            throw new InvalidOperationException("could not re-equip Emberweave Coat");
        }

        var resistantFire = _player.ApplyDamage(new DamageRequest(
            20,
            DamageType.Fire,
            "inventory_equipment_fire_resistance",
            CombatFaction.Enemy));
        _player.ApplyRestoredHealth(_player.MaxHealth);
        if (baselineFire.DamageApplied <= resistantFire.DamageApplied)
        {
            throw new InvalidOperationException(
                $"Fire resistance did not change actual DamageResult baseline={baselineFire.DamageApplied} resistant={resistantFire.DamageApplied}");
        }

        var atomicInventory = new RunInventory(1);
        var atomicEquipment = new Equipment();
        var atomicWeapon = generator.GenerateItemDrop(new ItemRollContext(
            1, LootSourceKind.Reward, EquipmentSlot.Weapon, ForcedRarity: Rarity.Magic));
        var atomicFiller = generator.GenerateItemDrop(new ItemRollContext(
            1, LootSourceKind.Reward, EquipmentSlot.Ring, ForcedRarity: Rarity.Magic));
        if (!atomicInventory.TryAdd(atomicWeapon, atomicEquipment)
            || !atomicInventory.TryEquip(atomicWeapon.Id, atomicEquipment).Succeeded
            || !atomicInventory.TryAdd(atomicFiller, atomicEquipment)
            || atomicInventory.TryUnequip(EquipmentSlot.Weapon, atomicEquipment).Succeeded
            || atomicEquipment.ItemInSlot(EquipmentSlot.Weapon)?.Id != atomicWeapon.Id
            || atomicInventory.Items.Single().Id != atomicFiller.Id)
        {
            throw new InvalidOperationException("full-inventory unequip rejection was not atomic");
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
            || !screen.DetailsText.Contains("Compare", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(_build.CompareSelectedToSlot()))
        {
            throw new InvalidOperationException("inventory UI did not present build data");
        }

        if (!_build.Close() || GetTree().Paused)
        {
            throw new InvalidOperationException("build screen did not close and resume the map");
        }
    }
}
