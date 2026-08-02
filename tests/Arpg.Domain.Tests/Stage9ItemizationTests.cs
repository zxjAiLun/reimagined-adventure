using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class Stage9ItemizationTests
{
    [Theory]
    [InlineData(EquipmentSlot.Weapon, "rustbound_blade")]
    [InlineData(EquipmentSlot.Armor, "ironhide_vest")]
    [InlineData(EquipmentSlot.Ring, "hunters_loop")]
    [InlineData(EquipmentSlot.Amulet, "vanguard_charm")]
    public void EveryEquipmentSlotHasAValidBase(EquipmentSlot slot, string baseId)
    {
        var definition = ItemBaseLibrary.Find(baseId);

        Assert.NotNull(definition);
        Assert.Equal(slot, definition.Slot);
        definition.Validate();
    }

    [Fact]
    public void GenericGeneratorHonorsRarityContractsAndAffixLevel()
    {
        var generator = new LootGenerator(9001);
        var normal = generator.GenerateItemDrop(new ItemRollContext(
            1, LootSourceKind.Reward, EquipmentSlot.Armor, ForcedRarity: Rarity.Normal));
        var magic = generator.GenerateItemDrop(new ItemRollContext(
            1, LootSourceKind.Reward, EquipmentSlot.Ring, ForcedRarity: Rarity.Magic));
        var rare = generator.GenerateItemDrop(new ItemRollContext(
            4, LootSourceKind.Reward, EquipmentSlot.Amulet, ForcedRarity: Rarity.Rare));
        var unique = generator.GenerateItemDrop(new ItemRollContext(
            1, LootSourceKind.Boss, EquipmentSlot.Weapon, GuaranteedUnique: true));

        Assert.Empty(normal.Affixes);
        Assert.Single(magic.Affixes);
        Assert.Equal(Rarity.Rare, rare.Rarity);
        Assert.Equal(2, rare.Affixes.Count);
        Assert.All(rare.Affixes, affix => Assert.True(affix.Tier <= 2));
        Assert.Equal(Rarity.Unique, unique.Rarity);
        Assert.Empty(unique.Affixes);
        normal.Validate();
        magic.Validate();
        rare.Validate();
        unique.Validate();
    }

    [Fact]
    public void SameSeedProducesIdenticalCompleteGenericItems()
    {
        var left = new LootGenerator(0xBEEF);
        var right = new LootGenerator(0xBEEF);
        var context = new ItemRollContext(4, LootSourceKind.Reward, RarityMultiplier: 2.0, AtlasMapId: "map", EncounterId: "encounter");

        for (var index = 0; index < 12; index++)
        {
            var leftItem = left.GenerateItemDrop(context);
            var rightItem = right.GenerateItemDrop(context);

            Assert.Equal(leftItem.Id, rightItem.Id);
            Assert.Equal(leftItem.BaseId, rightItem.BaseId);
            Assert.Equal(leftItem.Slot, rightItem.Slot);
            Assert.Equal(leftItem.Rarity, rightItem.Rarity);
            Assert.True(leftItem.Stats.EquivalentTo(rightItem.Stats));
            Assert.Equal(leftItem.Affixes.Select(affix => affix.Id), rightItem.Affixes.Select(affix => affix.Id));
        }
    }

    [Fact]
    public void AffixCandidatesNeverReturnAnIllegalSlot()
    {
        foreach (var slot in Enum.GetValues<EquipmentSlot>())
        {
            foreach (var definition in AffixLibrary.Candidates(slot, 4, true).Concat(AffixLibrary.Candidates(slot, 4, false)))
            {
                Assert.Contains(slot, definition.AllowedSlots);
                Assert.True(definition.MinimumItemLevel <= 4);
            }
        }
    }

    [Fact]
    public void InventoryEquipAndUnequipAreAtomic()
    {
        var generator = new LootGenerator(9010);
        var first = generator.GenerateWeaponDrop();
        var second = generator.GenerateItemDrop(new ItemRollContext(1, LootSourceKind.Reward, EquipmentSlot.Armor, ForcedRarity: Rarity.Magic));
        var inventory = new RunInventory(1);
        var equipment = new Equipment();

        Assert.True(inventory.TryAdd(first, equipment));
        Assert.False(inventory.TryAdd(second, equipment));
        var equip = inventory.TryEquip(first.Id, equipment);
        Assert.True(equip.Succeeded, equip.Error);
        Assert.Empty(inventory.Items);
        Assert.Same(first, equipment.ItemInSlot(EquipmentSlot.Weapon));

        Assert.True(inventory.TryAdd(second, equipment));
        Assert.False(inventory.TryUnequip(EquipmentSlot.Weapon, equipment).Succeeded);
        Assert.Same(first, equipment.ItemInSlot(EquipmentSlot.Weapon));
        Assert.Same(second, inventory.Remove(second.Id));
        var unequip = inventory.TryUnequip(EquipmentSlot.Weapon, equipment);
        Assert.True(unequip.Succeeded, unequip.Error);
        Assert.Same(first, inventory.Items[0]);
        Assert.Null(equipment.ItemInSlot(EquipmentSlot.Weapon));
    }

    [Fact]
    public void CurrencyRejectsInvalidSpendsAndSupportsRestore()
    {
        var wallet = new RunCurrencyWallet();

        Assert.True(wallet.TryAdd(3));
        Assert.False(wallet.TrySpend(4));
        Assert.Equal(3, wallet.ForgeFragments);
        Assert.True(wallet.TrySpend(2));
        Assert.Equal(1, wallet.ForgeFragments);
        Assert.False(wallet.Restore(-1));
        Assert.True(wallet.Restore(7));
        Assert.Equal(7, wallet.ForgeFragments);
    }

    [Fact]
    public void VeryHighItemLevelRemainsFiniteAndUsesTierTwo()
    {
        var item = new LootGenerator(9011).GenerateItemDrop(new ItemRollContext(
            int.MaxValue,
            LootSourceKind.Boss,
            EquipmentSlot.Amulet,
            GuaranteedUnique: false,
            ForcedRarity: Rarity.Rare));

        item.Validate();
        Assert.Equal(int.MaxValue, item.ItemLevel);
        Assert.All(item.Affixes, affix => Assert.InRange(affix.Tier, 1, 2));
    }
}
