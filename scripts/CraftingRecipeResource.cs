using Arpg.Domain;
using Godot;

[GlobalClass]
public partial class CraftingRecipeResource : Resource
{
    [Export] public string RecipeId { get; set; } = "reforge_weapon";
    [Export] public string DisplayName { get; set; } = "Reforge Weapon";
    [Export] public int Slot { get; set; } = (int)EquipmentSlot.Weapon;
    [Export] public string RequiredBaseId { get; set; } = string.Empty;
    [Export] public int ForgeFragmentCost { get; set; } = 1;

    public CraftingRecipe ToDomain()
    {
        var recipe = new CraftingRecipe
        {
            Id = RecipeId,
            Name = DisplayName,
            Slot = (EquipmentSlot)Slot,
            RequiredBaseId = string.IsNullOrWhiteSpace(RequiredBaseId) ? null : RequiredBaseId,
            ForgeFragmentCost = ForgeFragmentCost,
        };
        recipe.Validate();
        return recipe;
    }
}
