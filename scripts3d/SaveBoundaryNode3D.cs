using System;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// 3D adapter for the existing validated MinimalSaveService. The payload and
/// validation stay shared with the 2D slice; only node lookup is 3D-specific.
/// </summary>
public partial class SaveBoundaryNode3D : Node
{
    private readonly MinimalSaveService _service = new();
    private PlayerController3D _player;
    private GameFlowController3D _flow;
    private RunSessionNode _runSession;

    public override void _Ready()
    {
        _player = GetNodeOrNull<PlayerController3D>("../Player3D");
        _flow = GetNodeOrNull<GameFlowController3D>("../GameFlow3D");
        _runSession = GetTree().GetFirstNodeInGroup("run_sessions") as RunSessionNode;
        AddToGroup("save_boundaries_3d");
    }

    public bool TrySaveCurrentRun(out string error)
    {
        if (_player == null)
        {
            error = "3D player is not ready";
            return false;
        }

        var items = _player.Items.ToArray();
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
            InventoryItemIds = items.Select(item => item.Id).ToArray(),
            InventoryItems = items,
            EquippedWeaponId = _player.EquippedWeapon?.Id,
            EquippedWeapon = _player.EquippedWeapon,
        };
        return _service.TrySave(state, out error);
    }

    public bool TryLoadAndApplyLastRun(out MinimalRunState state, out string error)
    {
        state = null;
        if (!_service.TryLoad(out state, out error))
        {
            return false;
        }

        if (_player == null
            || state.InventoryItems.Count == 0 && state.InventoryItemIds.Count > 0
            || !state.InventoryItems.All(item => item != null)
            || !_player.CanRestoreCurrentHealth(state.PlayerCurrentHealth)
            || _runSession != null && !_runSession.CanRestore(
                state.RunSeed, state.ItemSequence, state.MapLevel))
        {
            error = "saved 3D content cannot be applied to the current scene";
            return false;
        }

        try
        {
            if (!_player.RestoreInventory(state.InventoryItems, state.EquippedWeapon))
            {
                throw new InvalidOperationException("saved 3D inventory is invalid");
            }

            _player.SetRewardStats(state.RewardStats);
            if (_runSession != null && !_runSession.TryRestore(
                    state.RunSeed,
                    state.ItemSequence,
                    state.MapLevel,
                    state.LootRandomState,
                    state.CraftingRandomState,
                    state.EventRandomState))
            {
                throw new InvalidOperationException("saved 3D run session is invalid");
            }

            _player.ApplyRestoredHealth(state.PlayerCurrentHealth);
            if (_flow != null && !_flow.RestoreState(ToGameFlowState(state.State)))
            {
                throw new InvalidOperationException("saved 3D game flow is invalid");
            }

            error = string.Empty;
            return true;
        }
        catch (ArgumentException exception)
        {
            error = $"saved 3D content could not be applied: {exception.Message}";
            return false;
        }
        catch (InvalidOperationException exception)
        {
            error = $"saved 3D content could not be applied: {exception.Message}";
            return false;
        }
    }

    private static SaveRunState ToSaveState(GameFlowState state) => state switch
    {
        GameFlowState.GameOver => SaveRunState.GameOver,
        GameFlowState.MapComplete => SaveRunState.MapComplete,
        _ => SaveRunState.Playing,
    };

    private static GameFlowState ToGameFlowState(SaveRunState state) => state switch
    {
        SaveRunState.GameOver => GameFlowState.GameOver,
        SaveRunState.MapComplete => GameFlowState.MapComplete,
        _ => GameFlowState.Playing,
    };
}
