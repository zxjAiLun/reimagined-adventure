using Arpg.Domain;
using Godot;

public partial class Milestone19FullSaveSmoke : Node2D
{
    public override void _Ready()
    {
        var player = GetNode<PlayerController>("Player");
        var inventory = player.Inventory;
        var passiveTree = player.GetNode<PassiveTreeNode>("PassiveTree");
        var atlas = GetNode<AtlasNode>("Atlas");
        var save = GetNode<SaveBoundaryNode>("SaveBoundary");
        var generator = new LootGenerator(19019);
        var inventoryItem = generator.GenerateWeaponDrop(2);
        var equippedItem = generator.GenerateWeaponDrop(2);

        var prepared = inventory.TryAddItem(inventoryItem)
            && inventory.TryAddItem(equippedItem)
            && inventory.TryEquipItem(1)
            && passiveTree.TryAllocate(0)
            && atlas.TryCompleteMap("quiet-coast");
        if (!prepared)
        {
            Fail("could not prepare content state");
            return;
        }

        var savedEquippedId = inventory.EquippedWeapon?.Id;
        var saveSucceeded = save.TrySaveCurrentRun(out var saveError);
        player.ApplyDamage(new DamageRequest(20, DamageType.Physical, "milestone19_mutation"));
        inventory.TryEquipItem(0);
        passiveTree.TryRestore(System.Array.Empty<int>());

        var applied = save.TryLoadAndApplyLastRun(out var restored, out var loadError);
        var valid = saveSucceeded
            && applied
            && restored.InventoryItems.Count == 1
            && restored.EquippedWeapon?.Id == savedEquippedId
            && inventory.EquippedWeapon?.Id == savedEquippedId
            && inventory.ItemCount == 1
            && player.MaxHealth == 100
            && player.CurrentHealth == 100
            && passiveTree.State.IsAllocated(0)
            && atlas.State.IsCompleted("quiet-coast");

        new MinimalSaveService().Delete();
        if (!valid)
        {
            Fail($"save={saveError} load={loadError}");
            return;
        }

        GD.Print("MILESTONE19_FULL_SAVE_PASS items=true passive=true atlas=true");
        GetTree().Quit(0);
    }

    private void Fail(string detail)
    {
        GD.PrintErr($"MILESTONE19_FULL_SAVE_FAIL {detail}");
        new MinimalSaveService().Delete();
        GetTree().Quit(1);
    }
}
