using System;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Scene-side adapter for the validated run save contract. File I/O remains in
/// Godot, while the snapshot and content payloads remain data-only.
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

        var passiveTree = _player.GetNodeOrNull<PassiveTreeNode>("PassiveTree");
        var atlas = GetNodeOrNull<AtlasNode>("../Atlas");

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
            InventoryItems = _inventory.Items.ToArray(),
            EquippedWeaponId = _inventory.EquippedWeapon?.Id,
            EquippedWeapon = _inventory.EquippedWeapon,
            PassiveAllocatedIndices = passiveTree?.State.AllocatedIndices ?? Array.Empty<int>(),
            AtlasUnlockedMapIds = atlas?.State.UnlockedMapIds.ToArray() ?? Array.Empty<string>(),
            AtlasCompletedMapIds = atlas?.State.CompletedMapIds.ToArray() ?? Array.Empty<string>(),
        };
        return _service.TrySave(state, out error);
    }

    public bool TryLoadLastRun(out MinimalRunState state, out string error)
    {
        return _service.TryLoad(out state, out error);
    }

    public bool TryLoadAndApplyLastRun(out MinimalRunState state, out string error)
    {
        if (!_service.TryLoad(out state, out error))
        {
            return false;
        }

        var passiveTree = _player?.GetNodeOrNull<PassiveTreeNode>("PassiveTree");
        var atlas = GetNodeOrNull<AtlasNode>("../Atlas");
        if (_player == null
            || _inventory == null
            || state.InventoryItems.Count == 0 && state.InventoryItemIds.Count > 0
            || !_inventory.TryRestoreSavedState(state)
            || !_player.TryRestoreCurrentHealth(state.PlayerCurrentHealth)
            || passiveTree != null && !passiveTree.TryRestore(state.PassiveAllocatedIndices)
            || atlas != null
                && (state.AtlasUnlockedMapIds.Count > 0 || state.AtlasCompletedMapIds.Count > 0)
                && !atlas.TryRestore(state.AtlasUnlockedMapIds, state.AtlasCompletedMapIds))
        {
            error = "saved content could not be applied to the current scene";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
