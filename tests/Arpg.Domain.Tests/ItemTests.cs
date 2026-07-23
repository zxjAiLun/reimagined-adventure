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

        Assert.Throws<ArgumentException>(() => item.Validate());
    }

    [Fact]
    public void ForgedStatsDoNotPassValidation()
    {
        var generated = new LootGenerator(43).GenerateWeaponDrop();
        var forged = new Item
        {
            Id = generated.Id,
            Name = generated.Name,
            BaseId = generated.BaseId,
            Slot = generated.Slot,
            Rarity = generated.Rarity,
            ItemLevel = generated.ItemLevel,
            RequiredLevel = generated.RequiredLevel,
            Stats = Stats.Neutral,
            Affixes = generated.Affixes,
        };

        Assert.Throws<ArgumentException>(() => forged.Validate());
    }

    [Fact]
    public void DuplicateAffixClassIsRejected()
    {
        var generated = new LootGenerator(44).GenerateWeaponDrop();
        var duplicate = generated.Affixes[0];
        var forged = new Item
        {
            Id = generated.Id,
            Name = generated.Name,
            BaseId = generated.BaseId,
            Slot = generated.Slot,
            Rarity = generated.Rarity,
            ItemLevel = generated.ItemLevel,
            RequiredLevel = generated.RequiredLevel,
            Stats = Stats.Combine(generated.Stats, duplicate.Stats),
            Affixes = [duplicate, duplicate],
        };

        Assert.Throws<ArgumentException>(() => forged.Validate());
    }
}
