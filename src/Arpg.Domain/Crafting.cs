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

        if (Slot != EquipmentSlot.Weapon)
        {
            throw new ArgumentException("The first crafting batch only supports weapon recipes.", nameof(Slot));
        }

        if (RequiredBaseId != null && string.IsNullOrWhiteSpace(RequiredBaseId))
        {
            throw new ArgumentException("Required base id cannot be blank.", nameof(RequiredBaseId));
        }

        if (RequiredBaseId != null && ItemBaseLibrary.Find(RequiredBaseId) == null)
        {
            throw new ArgumentException($"Unknown required weapon base '{RequiredBaseId}'.", nameof(RequiredBaseId));
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

        var crafted = generator.GenerateWeaponDropForBase(input.BaseId, input.ItemLevel, input.Id);
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
        Slot = EquipmentSlot.Weapon,
        RequiredBaseId = baseId,
        ForgeFragmentCost = 1,
    };
}
