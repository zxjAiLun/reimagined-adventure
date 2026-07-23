using Arpg.Domain;
using Godot;

public partial class Milestone10ContentBatchSmoke : Node
{
    public override void _Ready()
    {
        var stashNode = GetNode<StashNode>("Stash");
        var benchNode = GetNode<CraftingBenchNode>("CraftingBench");
        var atlasNode = GetNode<AtlasNode>("Atlas");
        var generator = new LootGenerator(2501);
        var input = generator.GenerateWeaponDrop(itemLevel: 2);

        var deposited = stashNode.TryDeposit("default", input);
        var crafted = benchNode.TryCraft(input, forgeFragments: 1);
        var progressed = atlasNode.TryCompleteMap("quiet-coast")
            && atlasNode.State.IsUnlocked("hardened-frontier");

        if (!deposited || !crafted || !progressed
            || benchNode.LastResult == null
            || benchNode.LastResult.CraftedItem.BaseId != input.BaseId)
        {
            GD.PrintErr("MILESTONE10_CONTENT_BATCH_FAIL");
            GetTree().Quit(1);
            return;
        }

        GD.Print("MILESTONE10_CONTENT_BATCH_PASS");
        GetTree().Quit(0);
    }
}
