using System;
using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// 3D adapter for the existing validated MinimalSaveService. The payload and
/// validation stay shared with the 2D slice; only node lookup is 3D-specific.
/// </summary>
public partial class SaveBoundaryNode3D : Node
{
    /// <summary>
    /// Test-only fault injection used by the recovery smoke to prove that a
    /// failed load rolls back mutations already applied to the 3D run.
    /// </summary>
    public bool InjectFailureAfterRewardForTest { get; set; }

    private readonly MinimalSaveService _service = new();
    private PlayerController3D _player;
    private GameFlowController3D _flow;
    private MapRewardNode3D _mapRewards;
    private BuildIntermissionController3D _buildIntermission;
    private RunSessionNode _runSession;

    public override void _Ready()
    {
        _player = GetNodeOrNull<PlayerController3D>("../Player3D");
        _flow = GetNodeOrNull<GameFlowController3D>("../GameFlow3D");
        _mapRewards = GetNodeOrNull<MapRewardNode3D>("../MapRewards3D");
        _buildIntermission = GetNodeOrNull<BuildIntermissionController3D>("../BuildIntermission3D");
        _runSession = MapRuntimeScope3D.FindRunSession(this);
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
            TotalExperience = _runSession?.TotalExperience ?? 0,
            AllocatedPassiveNodeIds = _runSession?.PassiveTree.AllocatedNodeIds.ToArray()
                ?? Array.Empty<string>(),
            InventoryItemIds = items.Select(item => item.Id).ToArray(),
            InventoryItems = items,
            EquippedWeaponId = _player.EquippedWeapon?.Id,
            EquippedWeapon = _player.EquippedWeapon,
            EquippedItemsBySlot = _player.EquippedItems.ToDictionary(pair => pair.Key, pair => pair.Value),
            UnlockedSupportIds = _player.Skills?.UnlockedSupportIds?.ToArray()
                ?? SkillLoadout.DefaultUnlockedSupportIds.ToArray(),
            SupportIdBySkillSlot = _player.Skills?.SupportIdBySkillSlot?.ToDictionary(pair => pair.Key, pair => pair.Value)
                ?? new Dictionary<SkillSlot, string>(),
            ForgeFragments = _buildIntermission?.Currency.ForgeFragments ?? 0,
            StashItems = _buildIntermission?.StashItems.ToArray() ?? Array.Empty<Item>(),
            MapCompletePhase = _flow?.State == GameFlowState.MapComplete
                ? _buildIntermission?.Phase ?? MapCompletePhase.RewardChoice
                : MapCompletePhase.RewardChoice,
            CurrentAtlasMapId = _runSession?.CurrentAtlasMapId ?? "quiet-coast",
            PendingAtlasMapId = _runSession?.PendingAtlasMapId,
            AtlasUnlockedMapIds = _runSession?.Atlas?.State.UnlockedMapIds.ToArray()
                ?? Array.Empty<string>(),
            AtlasCompletedMapIds = _runSession?.Atlas?.State.CompletedMapIds.ToArray()
                ?? Array.Empty<string>(),
            RouteSelectionCount = _runSession?.RouteSelectionCount ?? 0,
            SelectedMapRewardOption = _flow?.State == GameFlowState.MapComplete
                && _mapRewards?.HasChosen == true
                ? _mapRewards.ChosenRewardIndex
                : -1,
            MapRewardChosen = _flow?.State == GameFlowState.MapComplete
                && _mapRewards?.HasChosen == true,
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

        var targetPassiveStats = Stats.Neutral;
        if (_runSession != null
            && !_runSession.CanRestoreProgression(
                state.TotalExperience,
                state.AllocatedPassiveNodeIds,
                out targetPassiveStats))
        {
            error = "saved 3D passive progression is invalid";
            return false;
        }

        if (_runSession == null
            && (state.TotalExperience != 0 || state.AllocatedPassiveNodeIds.Count > 0))
        {
            error = "saved 3D passive progression has no run owner";
            return false;
        }

        if (_player == null
            || state.InventoryItems.Count == 0 && state.InventoryItemIds.Count > 0
            || !state.InventoryItems.All(item => item != null)
            || !TryGetEquippedItems(state, out var targetEquipment)
            || !_player.TryCalculateMaxHealthForRestore(
                state.InventoryItems,
                targetEquipment,
                state.RewardStats,
                targetPassiveStats,
                out var targetMaxHealth)
            || _player.Skills == null
            || !_player.Skills.CanRestoreLoadout(state.UnlockedSupportIds, state.SupportIdBySkillSlot)
            || _buildIntermission != null
                && !_buildIntermission.CanRestore(
                    state.StashItems,
                    state.ForgeFragments,
                    state.MapCompletePhase,
                    state.InventoryItems,
                    targetEquipment)
            || state.PlayerMaxHealth != targetMaxHealth
            || state.PlayerCurrentHealth < 0
            || state.PlayerCurrentHealth > targetMaxHealth
            || _runSession != null && (_runSession.UsesLegacyPlanResolution
                ? !_runSession.CanRestore(
                    state.RunSeed,
                    state.ItemSequence,
                    state.MapLevel)
                : !_runSession.CanRestore(
                    state.RunSeed,
                    state.ItemSequence,
                    state.MapLevel,
                    state.CurrentAtlasMapId,
                    state.PendingAtlasMapId,
                    state.AtlasUnlockedMapIds,
                    state.AtlasCompletedMapIds,
                    state.RouteSelectionCount)))
        {
            error = "saved 3D content cannot be applied to the current scene";
            return false;
        }

        var previousState = CaptureCurrentState();
        try
        {
            if (!_player.RestoreEquipment(state.InventoryItems, targetEquipment))
            {
                throw new InvalidOperationException("saved 3D inventory is invalid");
            }

            _player.SetRewardStats(state.RewardStats);
            if (_runSession != null
                && !_runSession.TryRestoreProgression(
                    state.TotalExperience,
                    state.AllocatedPassiveNodeIds))
            {
                throw new InvalidOperationException("saved 3D passive progression is invalid");
            }

            if (!_player.Skills.TryRestoreLoadout(state.UnlockedSupportIds, state.SupportIdBySkillSlot))
            {
                throw new InvalidOperationException("saved 3D skill loadout is invalid");
            }

            if (_buildIntermission != null
                && !_buildIntermission.RestoreState(state.StashItems, state.ForgeFragments, state.MapCompletePhase))
            {
                throw new InvalidOperationException("saved 3D build intermission is invalid");
            }
            if (InjectFailureAfterRewardForTest)
            {
                throw new InvalidOperationException("injected 3D restore failure");
            }

            if (_runSession != null && (_runSession.UsesLegacyPlanResolution
                ? !_runSession.TryRestore(
                    state.RunSeed,
                    state.ItemSequence,
                    state.MapLevel,
                    state.LootRandomState,
                    state.CraftingRandomState,
                    state.EventRandomState)
                : !_runSession.TryRestore(
                    state.RunSeed,
                    state.ItemSequence,
                    state.MapLevel,
                    state.LootRandomState,
                    state.CraftingRandomState,
                    state.EventRandomState,
                    state.CurrentAtlasMapId,
                    state.PendingAtlasMapId,
                    state.AtlasUnlockedMapIds,
                    state.AtlasCompletedMapIds,
                    state.RouteSelectionCount)))
            {
                throw new InvalidOperationException("saved 3D run session is invalid");
            }

            _player.ApplyRestoredRuntimeState(state.PlayerCurrentHealth);
            if (_flow != null && !_flow.RestoreState(ToGameFlowState(state.State)))
            {
                throw new InvalidOperationException("saved 3D game flow is invalid");
            }

            if (_mapRewards != null && state.State == SaveRunState.MapComplete)
            {
                if (state.MapRewardChosen)
                {
                    if (!_mapRewards.TryRestoreChoice(state.SelectedMapRewardOption))
                    {
                        throw new InvalidOperationException("saved 3D reward choice is invalid");
                    }
                }
                else
                {
                    _mapRewards.BeginChoice();
                }
            }

            // Ailments are intentionally not part of the save contract. A
            // successful restore starts the current map's transient combat
            // state clean, just like a freshly instantiated map.
            _player.Ailments?.ResetPresentation();

            error = string.Empty;
            return true;
        }
        catch (ArgumentException exception)
        {
            error = RollbackAfterFailure(previousState, exception.Message);
            return false;
        }
        catch (InvalidOperationException exception)
        {
            error = RollbackAfterFailure(previousState, exception.Message);
            return false;
        }
    }

    private MinimalRunState CaptureCurrentState()
    {
        var items = _player.Items.ToArray();
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
            TotalExperience = _runSession?.TotalExperience ?? 0,
            AllocatedPassiveNodeIds = _runSession?.PassiveTree.AllocatedNodeIds.ToArray()
                ?? Array.Empty<string>(),
            InventoryItemIds = items.Select(item => item.Id).ToArray(),
            InventoryItems = items,
            EquippedWeaponId = _player.EquippedWeapon?.Id,
            EquippedWeapon = _player.EquippedWeapon,
            EquippedItemsBySlot = _player.EquippedItems.ToDictionary(pair => pair.Key, pair => pair.Value),
            UnlockedSupportIds = _player.Skills?.UnlockedSupportIds?.ToArray()
                ?? SkillLoadout.DefaultUnlockedSupportIds.ToArray(),
            SupportIdBySkillSlot = _player.Skills?.SupportIdBySkillSlot?.ToDictionary(pair => pair.Key, pair => pair.Value)
                ?? new Dictionary<SkillSlot, string>(),
            ForgeFragments = _buildIntermission?.Currency.ForgeFragments ?? 0,
            StashItems = _buildIntermission?.StashItems.ToArray() ?? Array.Empty<Item>(),
            MapCompletePhase = _flow?.State == GameFlowState.MapComplete
                ? _buildIntermission?.Phase ?? MapCompletePhase.RewardChoice
                : MapCompletePhase.RewardChoice,
            CurrentAtlasMapId = _runSession?.CurrentAtlasMapId ?? "quiet-coast",
            PendingAtlasMapId = _runSession?.PendingAtlasMapId,
            AtlasUnlockedMapIds = _runSession?.Atlas?.State.UnlockedMapIds.ToArray()
                ?? Array.Empty<string>(),
            AtlasCompletedMapIds = _runSession?.Atlas?.State.CompletedMapIds.ToArray()
                ?? Array.Empty<string>(),
            RouteSelectionCount = _runSession?.RouteSelectionCount ?? 0,
            SelectedMapRewardOption = _flow?.State == GameFlowState.MapComplete
                && _mapRewards?.HasChosen == true
                ? _mapRewards.ChosenRewardIndex
                : -1,
            MapRewardChosen = _flow?.State == GameFlowState.MapComplete
                && _mapRewards?.HasChosen == true,
        };
    }

    private string RollbackAfterFailure(MinimalRunState previousState, string applyError)
    {
        if (!TryRollback(previousState, out var rollbackError))
        {
            return $"saved 3D content could not be applied: {applyError}; rollback failed: {rollbackError}";
        }

        return $"saved 3D content could not be applied: {applyError}";
    }

    private bool TryRollback(MinimalRunState state, out string error)
    {
        try
        {
            if (!TryGetEquippedItems(state, out var equipment)
                || !_player.RestoreEquipment(state.InventoryItems, equipment))
            {
                throw new InvalidOperationException("could not restore inventory");
            }

            _player.SetRewardStats(state.RewardStats);
            if (_runSession != null
                && !_runSession.TryRestoreProgression(
                    state.TotalExperience,
                    state.AllocatedPassiveNodeIds))
            {
                throw new InvalidOperationException("could not restore passive progression");
            }

            if (!_player.Skills.TryRestoreLoadout(state.UnlockedSupportIds, state.SupportIdBySkillSlot))
            {
                throw new InvalidOperationException("could not restore skill loadout");
            }

            if (_buildIntermission != null
                && !_buildIntermission.RestoreState(state.StashItems, state.ForgeFragments, state.MapCompletePhase))
            {
                throw new InvalidOperationException("could not restore build intermission");
            }
            if (_runSession != null && (_runSession.UsesLegacyPlanResolution
                ? !_runSession.TryRestore(
                    state.RunSeed,
                    state.ItemSequence,
                    state.MapLevel,
                    state.LootRandomState,
                    state.CraftingRandomState,
                    state.EventRandomState)
                : !_runSession.TryRestore(
                    state.RunSeed,
                    state.ItemSequence,
                    state.MapLevel,
                    state.LootRandomState,
                    state.CraftingRandomState,
                    state.EventRandomState,
                    state.CurrentAtlasMapId,
                    state.PendingAtlasMapId,
                    state.AtlasUnlockedMapIds,
                    state.AtlasCompletedMapIds,
                    state.RouteSelectionCount)))
            {
                throw new InvalidOperationException("could not restore run session");
            }

            _player.ApplyRestoredRuntimeState(state.PlayerCurrentHealth);
            if (_flow != null && !_flow.RestoreState(ToGameFlowState(state.State)))
            {
                throw new InvalidOperationException("could not restore game flow");
            }

            if (_mapRewards != null && state.State == SaveRunState.MapComplete)
            {
                if (state.MapRewardChosen)
                {
                    _mapRewards.TryRestoreChoice(state.SelectedMapRewardOption);
                }
                else
                {
                    _mapRewards.BeginChoice();
                }
            }

            error = string.Empty;
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
        catch (InvalidOperationException exception)
        {
            error = exception.Message;
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

    private static bool TryGetEquippedItems(
        MinimalRunState state,
        out IReadOnlyDictionary<EquipmentSlot, Item> equippedItems)
    {
        equippedItems = state?.EquippedItemsBySlot?.ToDictionary(pair => pair.Key, pair => pair.Value)
            ?? new Dictionary<EquipmentSlot, Item>();
        if (state?.EquippedWeapon != null && !equippedItems.ContainsKey(EquipmentSlot.Weapon))
        {
            var migrated = equippedItems.ToDictionary(pair => pair.Key, pair => pair.Value);
            migrated[EquipmentSlot.Weapon] = state.EquippedWeapon;
            equippedItems = migrated;
        }

        return state != null;
    }
}
