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
    private RunSessionNode _runSession;

    public override void _Ready()
    {
        _player = GetNodeOrNull<PlayerController>("../Player");
        _inventory = _player?.GetNodeOrNull<InventoryController>("InventoryController");
        _flow = GetNodeOrNull<GameFlowController>("../GameFlowController");
        _runSession = GetTree().GetFirstNodeInGroup("run_sessions") as RunSessionNode;
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
            State = ToSaveState(_flow?.State ?? GameFlowState.Playing),
            RunSeed = _runSession?.Session.RunSeed ?? RandomService.DefaultSeed,
            ItemSequence = _runSession?.Session.ItemSequence ?? 0,
            MapLevel = _runSession?.Session.MapLevel ?? 1,
            LootRandomState = _runSession?.Session.LootRandom.State
                ?? RandomService.DeriveSeed(RandomService.DefaultSeed, 1),
            CraftingRandomState = _runSession?.Session.CraftingRandom.State
                ?? RandomService.DeriveSeed(RandomService.DefaultSeed, 2),
            EventRandomState = _runSession?.Session.EventRandom.State
                ?? RandomService.DeriveSeed(RandomService.DefaultSeed, 3),
            PlayerMaxHealth = _player.MaxHealth,
            PlayerCurrentHealth = _player.CurrentHealth,
            RewardStats = _player.RewardStats,
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
        if (_player == null || _inventory == null
            || state.InventoryItems.Count == 0 && state.InventoryItemIds.Count > 0
            || !_inventory.TryPrepareRestoreState(state, out var inventoryPlan)
            || !_player.CanRestoreCurrentHealth(state.PlayerCurrentHealth)
            || passiveTree != null && !passiveTree.CanRestore(state.PassiveAllocatedIndices)
            || atlas != null
                && (state.AtlasUnlockedMapIds.Count > 0 || state.AtlasCompletedMapIds.Count > 0)
                && !atlas.CanRestore(state.AtlasUnlockedMapIds, state.AtlasCompletedMapIds)
            || _runSession != null
                && !_runSession.CanRestore(
                    state.RunSeed,
                    state.ItemSequence,
                    state.MapLevel))
        {
            error = "saved content could not be applied to the current scene";
            return false;
        }

        var previousState = CaptureCurrentState(passiveTree, atlas);
        try
        {
            _inventory.ApplyRestorePlan(inventoryPlan);
            _player.SetRewardStats(state.RewardStats);
            if (passiveTree != null && !passiveTree.TryRestore(state.PassiveAllocatedIndices))
            {
                throw new InvalidOperationException("passive restore unexpectedly failed");
            }

            if (atlas != null
                && (state.AtlasUnlockedMapIds.Count > 0 || state.AtlasCompletedMapIds.Count > 0)
                && !atlas.TryRestore(state.AtlasUnlockedMapIds, state.AtlasCompletedMapIds))
            {
                throw new InvalidOperationException("atlas restore unexpectedly failed");
            }

            if (_runSession != null
                && !_runSession.TryRestore(
                    state.RunSeed,
                    state.ItemSequence,
                    state.MapLevel,
                    state.LootRandomState,
                    state.CraftingRandomState,
                    state.EventRandomState))
            {
                throw new InvalidOperationException("run session restore unexpectedly failed");
            }

            _player.ApplyRestoredHealth(state.PlayerCurrentHealth);
            if (_flow != null && ! _flow.RestoreState(ToGameFlowState(state.State)))
            {
                throw new InvalidOperationException("game flow restore unexpectedly failed");
            }

            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TryRollback(previousState, passiveTree, atlas);
            error = $"saved content could not be applied atomically: {exception.Message}";
            return false;
        }
    }

    private MinimalRunState CaptureCurrentState(PassiveTreeNode passiveTree, AtlasNode atlas)
    {
        return new MinimalRunState
        {
            State = ToSaveState(_flow?.State ?? GameFlowState.Playing),
            RunSeed = _runSession?.Session.RunSeed ?? RandomService.DefaultSeed,
            ItemSequence = _runSession?.Session.ItemSequence ?? 0,
            MapLevel = _runSession?.Session.MapLevel ?? 1,
            LootRandomState = _runSession?.Session.LootRandom.State
                ?? RandomService.DeriveSeed(RandomService.DefaultSeed, 1),
            CraftingRandomState = _runSession?.Session.CraftingRandom.State
                ?? RandomService.DeriveSeed(RandomService.DefaultSeed, 2),
            EventRandomState = _runSession?.Session.EventRandom.State
                ?? RandomService.DeriveSeed(RandomService.DefaultSeed, 3),
            PlayerMaxHealth = _player.MaxHealth,
            PlayerCurrentHealth = _player.CurrentHealth,
            RewardStats = _player.RewardStats,
            InventoryItemIds = _inventory.Items.Select(item => item.Id).ToArray(),
            InventoryItems = _inventory.Items.ToArray(),
            EquippedWeaponId = _inventory.EquippedWeapon?.Id,
            EquippedWeapon = _inventory.EquippedWeapon,
            PassiveAllocatedIndices = passiveTree?.State.AllocatedIndices ?? Array.Empty<int>(),
            AtlasUnlockedMapIds = atlas?.State.UnlockedMapIds.ToArray() ?? Array.Empty<string>(),
            AtlasCompletedMapIds = atlas?.State.CompletedMapIds.ToArray() ?? Array.Empty<string>(),
        };
    }

    private void TryRollback(MinimalRunState previousState, PassiveTreeNode passiveTree, AtlasNode atlas)
    {
        GetTree().Paused = false;
        if (_inventory.TryPrepareRestoreState(previousState, out var plan))
        {
            _inventory.ApplyRestorePlan(plan);
        }

        passiveTree?.TryRestore(previousState.PassiveAllocatedIndices);
        if (atlas != null)
        {
            atlas.TryRestore(previousState.AtlasUnlockedMapIds, previousState.AtlasCompletedMapIds);
        }

        _runSession?.TryRestore(
            previousState.RunSeed,
            previousState.ItemSequence,
            previousState.MapLevel,
            previousState.LootRandomState,
            previousState.CraftingRandomState,
            previousState.EventRandomState);
        _player.TryRestoreCurrentHealth(previousState.PlayerCurrentHealth);
        _flow?.RestoreState(ToGameFlowState(previousState.State));
    }

    private static SaveRunState ToSaveState(GameFlowState state)
    {
        return state switch
        {
            GameFlowState.GameOver => SaveRunState.GameOver,
            GameFlowState.MapComplete => SaveRunState.MapComplete,
            _ => SaveRunState.Playing,
        };
    }

    private static GameFlowState ToGameFlowState(SaveRunState state)
    {
        return state switch
        {
            SaveRunState.GameOver => GameFlowState.GameOver,
            SaveRunState.MapComplete => GameFlowState.MapComplete,
            _ => GameFlowState.Playing,
        };
    }
}
