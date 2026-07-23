using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class StashTests
{
    [Fact]
    public void DepositAndWithdrawPreserveItemIdentity()
    {
        var item = new LootGenerator(20).GenerateWeaponDrop();
        var stash = Stash.CreateDefault();

        Assert.True(stash.TryDeposit("default", item));
        var withdrawn = stash.Withdraw("default", item.Id);

        Assert.Same(item, withdrawn);
        Assert.False(stash.ContainsItemId(item.Id));
    }

    [Fact]
    public void SameItemIdCannotExistInTwoTabs()
    {
        var item = new LootGenerator(21).GenerateWeaponDrop();
        var stash = new Stash(
        [
            new StashTab { Id = "main", Name = "Main", Capacity = 2 },
            new StashTab { Id = "weapons", Name = "Weapons", Capacity = 2 },
        ]);

        Assert.True(stash.TryDeposit("main", item));
        Assert.False(stash.TryDeposit("weapons", item));
    }

    [Fact]
    public void FullTabRejectsAdditionalItems()
    {
        var generator = new LootGenerator(22);
        var stash = new Stash(
        [new StashTab { Id = "main", Name = "Main", Capacity = 1 }]);

        Assert.True(stash.TryDeposit("main", generator.GenerateWeaponDrop()));
        Assert.False(stash.TryDeposit("main", generator.GenerateWeaponDrop()));
    }
}
