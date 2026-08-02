using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class BuildcraftTransactionsTests
{
    [Fact]
    public void InventoryAndStashTransfersPreserveIdentityAndRejectFullDestination()
    {
        var generator = new LootGenerator(1301);
        var first = generator.GenerateWeaponDrop();
        var second = generator.GenerateItemDrop(new ItemRollContext(1, LootSourceKind.Reward, EquipmentSlot.Armor, ForcedRarity: Rarity.Magic));
        var inventory = new RunInventory(2);
        var equipment = new Equipment();
        var stash = new Stash([new StashTab { Id = "default", Name = "Default", Capacity = 1 }]);
        var transactions = new BuildcraftTransactions();

        Assert.True(inventory.TryAdd(first, equipment));
        Assert.True(transactions.TryMoveInventoryToStash(inventory, equipment, stash, "default", first.Id));
        Assert.True(stash.ContainsItemId(first.Id));
        Assert.True(inventory.TryAdd(second, equipment));
        Assert.False(transactions.TryMoveInventoryToStash(inventory, equipment, stash, "default", second.Id));
        Assert.True(inventory.Contains(second.Id));
        Assert.True(transactions.TryMoveStashToInventory(inventory, equipment, stash, "default", first.Id));
        Assert.True(inventory.Contains(first.Id));
        Assert.False(stash.ContainsItemId(first.Id));
    }

    [Fact]
    public void ReforgeIsGenericAndConsumesOneItemAndFragmentAtomically()
    {
        var run = new RunSession(1302);
        var input = run.CreateLootGenerator().GenerateItemDrop(new ItemRollContext(
            4, LootSourceKind.Reward, EquipmentSlot.Amulet, ForcedRarity: Rarity.Magic));
        var inventory = new RunInventory();
        var equipment = new Equipment();
        var stash = Stash.CreateDefault();
        var wallet = new RunCurrencyWallet(2);
        var transactions = new BuildcraftTransactions();
        Assert.True(inventory.TryAdd(input, equipment));
        var oldLoot = run.LootRandom.State;
        var oldEvent = run.EventRandom.State;
        var oldCraft = run.CraftingRandom.State;

        var success = transactions.TryReforge(
            CraftingLibrary.ReforgeBase(input.BaseId),
            input.Id,
            inventory,
            equipment,
            stash,
            wallet,
            run,
            out var result,
            out var error);

        Assert.True(success, error);
        Assert.NotNull(result);
        Assert.Equal(input.BaseId, result!.CraftedItem.BaseId);
        Assert.Equal(input.ItemLevel, result.CraftedItem.ItemLevel);
        Assert.NotEqual(input.Id, result.CraftedItem.Id);
        Assert.False(inventory.Contains(input.Id));
        Assert.True(inventory.Contains(result.CraftedItem.Id));
        Assert.Equal(1, wallet.ForgeFragments);
        Assert.Equal(oldLoot, run.LootRandom.State);
        Assert.Equal(oldEvent, run.EventRandom.State);
        Assert.NotEqual(oldCraft, run.CraftingRandom.State);
    }

    [Fact]
    public void FailedReforgeRollsBackItemCurrencyAndAllRandomStreams()
    {
        var run = new RunSession(1303);
        var input = run.CreateLootGenerator().GenerateWeaponDrop();
        var inventory = new RunInventory();
        var equipment = new Equipment();
        var stash = Stash.CreateDefault();
        var wallet = new RunCurrencyWallet(0);
        var transactions = new BuildcraftTransactions();
        Assert.True(inventory.TryAdd(input, equipment));
        var before = (
            ItemSequence: run.ItemSequence,
            LootRandom: run.LootRandom.State,
            CraftingRandom: run.CraftingRandom.State,
            EventRandom: run.EventRandom.State);

        Assert.False(transactions.TryReforge(
            CraftingLibrary.ReforgeWeapon(),
            input.Id,
            inventory,
            equipment,
            stash,
            wallet,
            run,
            out _,
            out _));

        Assert.True(inventory.Contains(input.Id));
        Assert.Equal(0, wallet.ForgeFragments);
        Assert.Equal(before.ItemSequence, run.ItemSequence);
        Assert.Equal(before.LootRandom, run.LootRandom.State);
        Assert.Equal(before.CraftingRandom, run.CraftingRandom.State);
        Assert.Equal(before.EventRandom, run.EventRandom.State);
    }
}
