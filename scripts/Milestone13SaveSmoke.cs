using Arpg.Domain;
using Godot;

public partial class Milestone13SaveSmoke : Node
{
    public override void _Ready()
    {
        var service = new MinimalSaveService();
        service.Delete();
        var original = new MinimalRunState
        {
            State = SaveRunState.MapComplete,
            MapLevel = 2,
            PlayerMaxHealth = 105,
            PlayerCurrentHealth = 91,
            ManaCharges = 2,
            InventoryItemIds = ["drop_0001", "drop_0002"],
            EquippedWeaponId = "drop_0001",
        };

        var saveSucceeded = service.TrySave(original, out var saveError);
        var loadSucceeded = service.TryLoad(out var restored, out var loadError);
        if (!saveSucceeded
            || !loadSucceeded
            || restored.State != original.State
            || restored.MapLevel != original.MapLevel
            || restored.PlayerCurrentHealth != original.PlayerCurrentHealth
            || restored.InventoryItemIds.Count != 2
            || restored.EquippedWeaponId != original.EquippedWeaponId)
        {
            GD.PrintErr($"MILESTONE13_SAVE_FAIL save={saveError} load={loadError}");
            service.Delete();
            GetTree().Quit(1);
            return;
        }

        service.Delete();
        GD.Print("MILESTONE13_SAVE_PASS");
        GetTree().Quit(0);
    }
}
