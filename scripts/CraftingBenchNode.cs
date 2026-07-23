using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// Minimal scene adapter for the first crafting batch. UI and currency
/// presentation can be layered on later without moving recipe rules here.
/// </summary>
public partial class CraftingBenchNode : Node
{
    [Signal]
    public delegate void CraftCompletedEventHandler(string itemId);

    [Export] public CraftingRecipeResource RecipeResource { get; set; }
    [Export] public int Seed { get; set; } = 2401;

    private readonly CraftingBench _bench = new();
    private LootGenerator _generator;

    public CraftingResult LastResult { get; private set; }

    public override void _Ready()
    {
        var session = GetTree().GetFirstNodeInGroup("run_sessions") as RunSessionNode;
        _generator = session?.CraftingGenerator
            ?? new LootGenerator((ulong)Mathf.Max(1, Seed));
        AddToGroup("crafting_benches");
    }

    public bool TryCraft(Item input, int forgeFragments)
    {
        var recipe = RecipeResource?.ToDomain() ?? CraftingLibrary.ReforgeWeapon();
        try
        {
            LastResult = _bench.Craft(recipe, input, forgeFragments, _generator);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        EmitSignal(SignalName.CraftCompleted, LastResult.CraftedItem.Id);
        return true;
    }

    public bool TryCraftInventoryItem(
        PlayerController player,
        int inventoryIndex,
        int forgeFragments,
        out Item craftedItem)
    {
        craftedItem = null;
        if (player?.Inventory == null
            || inventoryIndex < 0
            || inventoryIndex >= player.Inventory.ItemCount)
        {
            return false;
        }

        var input = player.Inventory.Items[inventoryIndex];
        if (!TryCraft(input, forgeFragments, out var result))
        {
            return false;
        }

        if (!player.Inventory.TryReplaceItem(inventoryIndex, result.CraftedItem))
        {
            return false;
        }

        craftedItem = result.CraftedItem;
        return true;
    }

    private bool TryCraft(Item input, int forgeFragments, out CraftingResult result)
    {
        result = null;
        var recipe = RecipeResource?.ToDomain() ?? CraftingLibrary.ReforgeWeapon();
        try
        {
            result = _bench.Craft(recipe, input, forgeFragments, _generator);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        EmitSignal(SignalName.CraftCompleted, result.CraftedItem.Id);
        return true;
    }
}
