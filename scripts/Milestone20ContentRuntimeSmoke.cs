using Arpg.Domain;
using Godot;

public partial class Milestone20ContentRuntimeSmoke : Node2D
{
    public override void _Ready()
    {
        var player = GetNode<PlayerController>("Player");
        var stash = GetNode<StashNode>("Stash");
        var bench = GetNode<CraftingBenchNode>("CraftingBench");
        var atlas = GetNode<AtlasNode>("Atlas");
        var item = new LootGenerator(20020).GenerateWeaponDrop(2);

        if (!player.Inventory.TryAddItem(item)
            || !stash.TryStoreInventoryItem("default", player, 0)
            || player.Inventory.ItemCount != 0
            || !stash.DomainStash.ContainsItemId(item.Id)
            || !stash.TryWithdrawToInventory("default", item.Id, player)
            || player.Inventory.ItemCount != 1
            || !bench.TryCraftInventoryItem(player, 0, 1, out var crafted)
            || crafted == null
            || player.Inventory.Items[0].Id != crafted.Id
            || player.Inventory.Items[0].BaseId != item.BaseId
            || !atlas.TryCompleteMap("quiet-coast"))
        {
            GD.PrintErr("MILESTONE20_CONTENT_RUNTIME_FAIL");
            GetTree().Quit(1);
            return;
        }

        GD.Print("MILESTONE20_CONTENT_RUNTIME_PASS stash=true crafting=true atlas=true");
        GetTree().Quit(0);
    }
}
