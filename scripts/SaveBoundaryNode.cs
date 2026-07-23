using Arpg.Domain;
using Godot;

/// <summary>
/// Scene-side adapter for the minimal save contract. It intentionally does
/// not recreate the complete world or inventory item payload yet.
/// </summary>
public partial class SaveBoundaryNode : Node
{
    private readonly MinimalSaveService _service = new();
    private PlayerController _player;
    private InventoryController _inventory;
    private GameFlowController _flow;

    public override void _Ready()
    {
        _player = GetNodeOrNull<PlayerController>("../Player");
        _inventory = _player?.GetNodeOrNull<InventoryController>("InventoryController");
        _flow = GetNodeOrNull<GameFlowController>("../GameFlowController");
        AddToGroup("save_boundaries");
    }

    public bool TrySaveCurrentRun(out string error)
    {
        if (_player == null || _inventory == null)
        {
            error = "player or inventory is not ready";
            return false;
        }

        var itemIds = new string[_inventory.ItemCount];
        for (var index = 0; index < _inventory.ItemCount; index++)
        {
            itemIds[index] = _inventory.Items[index].Id;
        }

        var state = new MinimalRunState
        {
            State = _flow?.State == GameFlowState.MapComplete
                ? SaveRunState.MapComplete
                : SaveRunState.Playing,
            MapLevel = 1,
            PlayerMaxHealth = _player.MaxHealth,
            PlayerCurrentHealth = _player.CurrentHealth,
            ManaCharges = SaveSnapshot.MaxManaCharges,
            InventoryItemIds = itemIds,
            EquippedWeaponId = _inventory.EquippedWeapon?.Id,
        };
        return _service.TrySave(state, out error);
    }

    public bool TryLoadLastRun(out MinimalRunState state, out string error)
    {
        return _service.TryLoad(out state, out error);
    }
}
