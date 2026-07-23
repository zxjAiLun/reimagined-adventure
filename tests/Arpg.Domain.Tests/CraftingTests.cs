using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class CraftingTests
{
    [Fact]
    public void ReforgeWeaponConsumesOneFragmentAndKeepsBase()
    {
        var input = new LootGenerator(30).GenerateWeaponDrop(itemLevel: 3);
        var recipe = CraftingLibrary.ReforgeWeapon();
        var result = new CraftingBench().Craft(recipe, input, forgeFragments: 1, new LootGenerator(30));

        Assert.Same(input, result.ConsumedItem);
        Assert.NotSame(input, result.CraftedItem);
        Assert.Equal(input.BaseId, result.CraftedItem.BaseId);
        Assert.Equal(input.ItemLevel, result.CraftedItem.ItemLevel);
        Assert.Equal(1, result.ForgeFragmentsSpent);
        Assert.NotEqual(input.Id, result.CraftedItem.Id);
    }

    [Fact]
    public void BaseRestrictedRecipeRejectsDifferentBaseAndInsufficientCurrency()
    {
        var input = new LootGenerator(32).GenerateWeaponDrop();
        var wrongBase = CraftingLibrary.ReforgeBase("brimstone_brand");
        var bench = new CraftingBench();

        Assert.False(wrongBase.CanCraft(input, forgeFragments: 1));
        Assert.Throws<InvalidOperationException>(() =>
            bench.Craft(CraftingLibrary.ReforgeWeapon(), input, forgeFragments: 0, new LootGenerator(33)));
    }

    [Fact]
    public void InvalidRecipeDataIsRejected()
    {
        var recipe = new CraftingRecipe
        {
            Id = "bad",
            Name = "Bad",
            ForgeFragmentCost = -1,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => recipe.Validate());
    }
}
