using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class LootEconomyTests
{
    [Fact]
    public void DropProfilesValidateAndBossGuaranteesItemAndFragments()
    {
        LootDropProfiles.Feral.Validate();
        LootDropProfiles.Spitter.Validate();
        LootDropProfiles.Boss.Validate();

        var result = new LootGenerator(4001).GenerateDrops(
            new ItemRollContext(3, LootSourceKind.Boss, GuaranteedUnique: true),
            LootDropProfiles.Boss);

        Assert.NotEmpty(result.Items);
        Assert.Equal(Rarity.Unique, result.Items[0].Rarity);
        Assert.Equal(3, result.ForgeFragments);
    }

    [Fact]
    public void QuantityAndRarityModifiersDoNotChangeItemLevelRule()
    {
        var baseModifier = new MapModifierStats { ItemQuantityMultiplier = 1.0, ItemRarityMultiplier = 1.0, ItemLevelBonus = 3 };
        var rareModifier = new MapModifierStats { ItemQuantityMultiplier = 4.0, ItemRarityMultiplier = 3.0, ItemLevelBonus = 3 };
        var profile = new LootDropProfile(100, 1, 0, 0, true);
        var baseResult = new LootGenerator(4002).GenerateDrops(
            new ItemRollContext(2, LootSourceKind.Reward, EquipmentSlot.Amulet), profile, baseModifier);
        var rareResult = new LootGenerator(4002).GenerateDrops(
            new ItemRollContext(2, LootSourceKind.Reward, EquipmentSlot.Amulet), profile, rareModifier);

        Assert.Single(baseResult.Items);
        Assert.Single(rareResult.Items);
        Assert.Equal(5, baseResult.Items[0].ItemLevel);
        Assert.Equal(baseResult.Items[0].ItemLevel, rareResult.Items[0].ItemLevel);
        Assert.Equal(EquipmentSlot.Amulet, rareResult.Items[0].Slot);
    }

    [Fact]
    public void QuantityMultiplierCanIncreaseSuccessfulDropChance()
    {
        var profile = new LootDropProfile(25, 1, 0, 0, false);
        var low = new LootGenerator(4003).GenerateDrops(
            new ItemRollContext(1, LootSourceKind.Reward, EquipmentSlot.Ring), profile);
        var high = new LootGenerator(4003).GenerateDrops(
            new ItemRollContext(1, LootSourceKind.Reward, EquipmentSlot.Ring), profile,
            new MapModifierStats { ItemQuantityMultiplier = 4.0 });

        Assert.Empty(low.Items);
        Assert.Single(high.Items);
    }

    [Fact]
    public void LootUsesOnlyLootStreamAndKeepsIdentitySequenceGlobal()
    {
        var left = new RunSession(4004);
        var right = new RunSession(4004);
        var profile = new LootDropProfile(100, 1, 0, 0, true);
        var leftLoot = left.CreateLootGenerator().GenerateDrops(
            new ItemRollContext(2, LootSourceKind.Reward), profile);
        var rightLoot = right.CreateLootGenerator().GenerateDrops(
            new ItemRollContext(2, LootSourceKind.Reward), profile);

        Assert.Equal(leftLoot.Items[0].Id, rightLoot.Items[0].Id);
        Assert.Equal(left.LootRandom.State, right.LootRandom.State);
        Assert.Equal(RandomService.DeriveSeed(4004, 2), left.CraftingRandom.State);
        Assert.Equal(RandomService.DeriveSeed(4004, 3), left.EventRandom.State);
        Assert.Equal(1, left.ItemSequence);
    }
}
