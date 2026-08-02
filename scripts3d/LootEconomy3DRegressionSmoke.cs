using System;
using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Stage 11 smoke: generates the real multi-slot Domain drops, instantiates
/// their world representations, and checks identity/currency contracts.
/// </summary>
public partial class LootEconomy3DRegressionSmoke : Node
{
    private bool _complete;
    private double _elapsed;

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

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
            GD.Print("LOOT_ECONOMY_3D_SPIKE_PASS deterministic=true quantity=true rarity=true item_level=true four_slots=true fragments=true identity=true");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            _complete = true;
            GD.PushError($"LOOT_ECONOMY_3D_SPIKE_FAIL {exception.Message}");
            GetTree().Quit(1);
        }
    }

    private void RunSmoke()
    {
        var session = new RunSession(0x11_11_11UL);
        var generator = session.CreateLootGenerator();
        var profile = new LootDropProfile(100, 1, 100, 1, true);
        var slots = Enum.GetValues<EquipmentSlot>();
        var drops = new List<Item>();
        var wallet = new RunCurrencyWallet();
        var craftingStateBefore = session.CraftingRandom.State;
        var eventStateBefore = session.EventRandom.State;

        foreach (var slot in slots)
        {
            var result = generator.GenerateDrops(
                new ItemRollContext(4, LootSourceKind.Reward, slot, RarityMultiplier: 1.0),
                profile,
                new MapModifierStats { ItemLevelBonus = 1 },
                Stats.Neutral);
            if (result.Items.Count != 1 || result.Items[0].Slot != slot || result.Items[0].ItemLevel != 5)
            {
                throw new InvalidOperationException($"slot {slot} drop contract failed");
            }

            drops.Add(result.Items[0]);
            if (!wallet.TryAdd(result.ForgeFragments))
            {
                throw new InvalidOperationException("forge fragment wallet rejected a valid drop");
            }
        }

        if (drops.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != drops.Count
            || wallet.ForgeFragments != 4
            || session.CraftingRandom.State != craftingStateBefore
            || session.EventRandom.State != eventStateBefore)
        {
            throw new InvalidOperationException("drop identity, fragment accumulation, or RNG isolation failed");
        }

        var quantityProfile = new LootDropProfile(25, 1, 0, 0, false);
        var lowQuantity = new LootGenerator(4003).GenerateDrops(
            new ItemRollContext(1, LootSourceKind.Reward, EquipmentSlot.Ring),
            quantityProfile,
            new MapModifierStats { ItemQuantityMultiplier = 1.0 });
        var highQuantity = new LootGenerator(4003).GenerateDrops(
            new ItemRollContext(1, LootSourceKind.Reward, EquipmentSlot.Ring),
            quantityProfile,
            new MapModifierStats { ItemQuantityMultiplier = 4.0 });
        if (lowQuantity.Items.Count != 0 || highQuantity.Items.Count != 1)
        {
            throw new InvalidOperationException("quantity multiplier did not change the real drop result");
        }

        var rarityChanged = false;
        for (var seed = 1UL; seed <= 10_000UL && !rarityChanged; seed++)
        {
            var lowRarity = new RunSession(seed).CreateLootGenerator().GenerateDrops(
                new ItemRollContext(1, LootSourceKind.Reward, EquipmentSlot.Amulet),
                new LootDropProfile(100, 1, 0, 0, true),
                new MapModifierStats { ItemRarityMultiplier = 1.0 });
            var highRarity = new RunSession(seed).CreateLootGenerator().GenerateDrops(
                new ItemRollContext(1, LootSourceKind.Reward, EquipmentSlot.Amulet),
                new LootDropProfile(100, 1, 0, 0, true),
                new MapModifierStats { ItemRarityMultiplier = 8.0 });
            rarityChanged = lowRarity.Items.Count == 1
                && highRarity.Items.Count == 1
                && highRarity.Items[0].Rarity > lowRarity.Items[0].Rarity;
        }

        if (!rarityChanged)
        {
            throw new InvalidOperationException("rarity multiplier did not change any deterministic real item roll");
        }

        var boss = new RunSession(0xB055UL).CreateLootGenerator().GenerateDrops(
            new ItemRollContext(4, LootSourceKind.Boss, GuaranteedUnique: true),
            LootDropProfiles.Boss);
        if (boss.Items.Count == 0
            || boss.Items[0].Rarity != Rarity.Unique
            || boss.ForgeFragments != LootDropProfiles.Boss.ForgeFragmentAmount)
        {
            throw new InvalidOperationException("Boss guaranteed unique and fragment contract failed");
        }

        var dropScene = GD.Load<PackedScene>("res://scenes3d/ItemDrop3D.tscn")
            ?? throw new InvalidOperationException("ItemDrop3D scene is missing");
        for (var index = 0; index < drops.Count; index++)
        {
            var node = dropScene.Instantiate<ItemDrop3D>();
            AddChild(node);
            node.Position = new Vector3(index * 1.5f, 0.0f, 0.0f);
            node.Configure(drops[index]);
        }

        var worldDrops = GetTree().GetNodesInGroup("item_drops_3d").OfType<ItemDrop3D>().ToArray();
        if (worldDrops.Length != 4
            || worldDrops.Any(drop => !drop.GetNode<Label3D>("Label").Text.Contains("Item Level 5", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("multi-slot world drop labels were not rendered");
        }

        var inventory = new RunInventory(1);
        var equipment = new Equipment();
        if (!inventory.TryAdd(drops[0], equipment)
            || inventory.CanAccept(drops[1], equipment)
            || worldDrops[1].Item == null)
        {
            throw new InvalidOperationException("full inventory did not preserve a world item");
        }
    }
}
