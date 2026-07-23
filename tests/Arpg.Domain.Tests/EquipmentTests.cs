using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class EquipmentTests
{
    [Fact]
    public void SameSlotEquipReturnsReplacedItemAndAggregatesCurrentStats()
    {
        var generator = new LootGenerator(7);
        var first = generator.GenerateWeaponDrop();
        var second = generator.GenerateWeaponDrop();
        var equipment = new Equipment();

        Assert.Null(equipment.Equip(first));
        var replaced = equipment.Equip(second);

        Assert.Same(first, replaced);
        Assert.Same(second, equipment.ItemInSlot(EquipmentSlot.Weapon));
        Assert.Equal(second.Stats.DamageMultiplier, equipment.CombinedStats().DamageMultiplier);
    }

    [Fact]
    public void UnknownBaseAndHighLevelItemCannotBeEquipped()
    {
        var equipment = new Equipment();
        var unknown = new Item
        {
            Id = "unknown-item",
            Name = "Unknown",
            BaseId = "missing",
            Slot = EquipmentSlot.Weapon,
        };
        var generated = new LootGenerator(1).GenerateWeaponDrop();
        var highLevel = new Item
        {
            Id = generated.Id,
            Name = generated.Name,
            BaseId = generated.BaseId,
            Slot = generated.Slot,
            Rarity = generated.Rarity,
            ItemLevel = generated.ItemLevel,
            RequiredLevel = 5,
            Stats = generated.Stats,
            Affixes = generated.Affixes,
        };

        Assert.False(equipment.CanEquip(unknown));
        Assert.False(equipment.CanEquip(highLevel, playerLevel: 1));
        Assert.Throws<InvalidOperationException>(() => equipment.Equip(unknown));
    }
}
