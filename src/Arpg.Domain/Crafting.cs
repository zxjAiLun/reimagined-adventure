namespace Arpg.Domain;

public sealed class CraftingRecipe
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public EquipmentSlot Slot { get; init; } = EquipmentSlot.Weapon;
    public string? RequiredBaseId { get; init; }
    public int ForgeFragmentCost { get; init; } = 1;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Crafting recipe id and name are required.");
        }

        if (!Enum.IsDefined(Slot))
        {
            throw new ArgumentOutOfRangeException(nameof(Slot), Slot, "Unknown crafting slot.");
        }

        if (RequiredBaseId != null && string.IsNullOrWhiteSpace(RequiredBaseId))
        {
            throw new ArgumentException("Required base id cannot be blank.", nameof(RequiredBaseId));
        }

        if (RequiredBaseId != null
            && (ItemBaseLibrary.Find(RequiredBaseId) == null
                || ItemBaseLibrary.Find(RequiredBaseId)!.Slot != Slot))
        {
            throw new ArgumentException($"Unknown required {Slot} base '{RequiredBaseId}'.", nameof(RequiredBaseId));
        }

        if (ForgeFragmentCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ForgeFragmentCost), "Crafting cost cannot be negative.");
        }
    }

    public bool CanCraft(Item item, int forgeFragments)
    {
        ArgumentNullException.ThrowIfNull(item);
        Validate();
        try
        {
            item.Validate();
        }
        catch (ArgumentException)
        {
            return false;
        }

        return item.Slot == Slot
            && (RequiredBaseId == null || item.BaseId == RequiredBaseId)
            && forgeFragments >= ForgeFragmentCost;
    }
}

public sealed record CraftingResult(Item ConsumedItem, Item CraftedItem, int ForgeFragmentsSpent);

public sealed class CraftingBench
{
    public CraftingResult Craft(
        CraftingRecipe recipe,
        Item input,
        int forgeFragments,
        LootGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(generator);
        recipe.Validate();
        input.Validate();
        if (!recipe.CanCraft(input, forgeFragments))
        {
            throw new InvalidOperationException($"Cannot craft recipe '{recipe.Name}' with the supplied input.");
        }

        var context = new ItemRollContext(
            input.ItemLevel,
            LootSourceKind.Crafting,
            input.Slot,
            ForcedBaseId: input.BaseId);
        var crafted = generator.GenerateItemDrop(context);
        while (crafted.Id == input.Id)
        {
            crafted = generator.GenerateItemDrop(context);
        }
        return new CraftingResult(input, crafted, recipe.ForgeFragmentCost);
    }
}

public static class CraftingLibrary
{
    public static CraftingRecipe ReforgeWeapon() => new()
    {
        Id = "reforge_weapon",
        Name = "Reforge Weapon",
        Slot = EquipmentSlot.Weapon,
        ForgeFragmentCost = 1,
    };

    public static CraftingRecipe ReforgeBase(string baseId) => new()
    {
        Id = $"reforge_{baseId}",
        Name = $"Reforge {baseId}",
        Slot = ItemBaseLibrary.Find(baseId)?.Slot ?? EquipmentSlot.Weapon,
        RequiredBaseId = baseId,
        ForgeFragmentCost = 1,
    };
}
