using Arpg.Domain;
using Godot;

public partial class Milestone9SaveBoundarySmoke : Node
{
    public override void _Ready()
    {
        var original = new MinimalRunState
        {
            State = SaveRunState.MapComplete,
            MapLevel = 2,
            PlayerMaxHealth = 110,
            PlayerCurrentHealth = 88,
            ManaCharges = 1,
            InventoryItemIds = ["drop_0001"],
            EquippedWeaponId = "drop_0001",
        };
        var restored = SaveSnapshot.Capture(original).Restore();
        if (restored.MapLevel != original.MapLevel
            || restored.PlayerCurrentHealth != original.PlayerCurrentHealth
            || restored.InventoryItemIds.Count != 1
            || restored.EquippedWeaponId != "drop_0001")
        {
            GD.PrintErr("MILESTONE9_SAVE_BOUNDARY_FAIL");
            GetTree().Quit(1);
            return;
        }

        GD.Print("MILESTONE9_SAVE_BOUNDARY_PASS");
        GetTree().Quit(0);
    }
}
