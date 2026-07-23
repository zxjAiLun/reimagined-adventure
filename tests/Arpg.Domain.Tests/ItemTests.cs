using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class ItemTests
{
    [Fact]
    public void GeneratedWeaponHasValidBaseAndAffix()
    {
        var item = new LootGenerator(42).GenerateWeaponDrop();

        item.Validate();
        Assert.Equal(EquipmentSlot.Weapon, item.Slot);
        Assert.NotEmpty(item.Affixes);
        Assert.NotNull(ItemBaseLibrary.Find(item.BaseId));
    }

    [Fact]
    public void InvalidItemDataIsRejected()
    {
        var item = new Item
        {
            Id = "bad",
            Name = "Bad Item",
            BaseId = "unknown",
            Slot = EquipmentSlot.Weapon,
            ItemLevel = 0,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => item.Validate());
    }
}
