using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class LootGeneratorTests
{
    [Fact]
    public void SameSeedProducesSameDropSequence()
    {
        var left = new LootGenerator(0x1234UL);
        var right = new LootGenerator(0x1234UL);

        for (var index = 0; index < 8; index++)
        {
            var leftItem = left.GenerateWeaponDrop(index + 1, boss: index == 7);
            var rightItem = right.GenerateWeaponDrop(index + 1, boss: index == 7);

            Assert.Equal(leftItem.Id, rightItem.Id);
            Assert.Equal(leftItem.BaseId, rightItem.BaseId);
            Assert.Equal(leftItem.Name, rightItem.Name);
            Assert.Equal(leftItem.Stats.DamageMultiplier, rightItem.Stats.DamageMultiplier);
            Assert.Equal(leftItem.Stats.ProjectileDamageMultiplier, rightItem.Stats.ProjectileDamageMultiplier);
        }
    }

    [Fact]
    public void BossDropIsGuaranteedUniqueWeapon()
    {
        var item = new LootGenerator(99).GenerateWeaponDrop(4, boss: true);

        Assert.Equal(Rarity.Unique, item.Rarity);
        Assert.Equal(EquipmentSlot.Weapon, item.Slot);
        Assert.Equal("brimstone_brand", item.BaseId);
        Assert.True(item.Stats.DamageMultiplier > 1.0);
    }

    [Fact]
    public void RunSessionSharesIdentityAcrossLootAndCraftingStreams()
    {
        var session = new RunSession(9017);
        var loot = session.CreateLootGenerator();
        var crafting = session.CreateCraftingGenerator();

        var first = loot.GenerateWeaponDrop();
        var second = crafting.GenerateWeaponDropForBase(first.BaseId, first.ItemLevel, first.Id);
        var third = loot.GenerateWeaponDrop(2, boss: true);

        Assert.Equal("item_00000001", first.Id);
        Assert.Equal("item_00000002", second.Id);
        Assert.Equal("item_00000003", third.Id);
        Assert.Equal(3, session.ItemSequence);
    }
}
