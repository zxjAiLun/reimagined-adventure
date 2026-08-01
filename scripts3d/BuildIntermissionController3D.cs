#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Owns the MapComplete intermission order and run-owned stash boundary.
/// It deliberately does not duplicate item or crafting rules.
/// </summary>
public partial class BuildIntermissionController3D : Node
{
    [Signal]
    public delegate void PhaseChangedEventHandler(int phase);

    [Signal]
    public delegate void BuildDataChangedEventHandler();

    public MapCompletePhase Phase { get; private set; } = MapCompletePhase.RewardChoice;
    public Stash Stash { get; } = Stash.CreateDefault();
    public RunCurrencyWallet Currency => _build?.Currency ?? _fallbackCurrency;
    public IReadOnlyList<Item> StashItems => Stash.Items;
    public bool IsBuildManagement => Phase == MapCompletePhase.BuildManagement;
    public bool IsRouteChoice => Phase == MapCompletePhase.RouteChoice;
    public string SelectedStashItemId => _selectedStashItemId;

    private readonly RunCurrencyWallet _fallbackCurrency = new();
    private readonly BuildcraftTransactions _transactions = new();
    private PlayerController3D _player = null!;
    private PlayerBuildController3D _build = null!;
    private MapRewardNode3D _rewards = null!;
    private GameFlowController3D _flow = null!;
    private RunSessionNode _run = null!;
    private string _selectedStashItemId = string.Empty;
    private bool _exiting;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        SetProcessUnhandledInput(true);
        AddToGroup("build_intermissions_3d");
        CallDeferred(nameof(BindRuntime));
    }

    public override void _ExitTree()
    {
        _exiting = true;
        if (_rewards != null)
        {
            _rewards.RewardChosen -= OnRewardChosen;
        }
    }

    public bool TryCompleteBuildForTest() => TryCompleteBuild();

    public bool TryCompleteBuild()
    {
        if (!IsBuildManagement || _flow?.State != GameFlowState.MapComplete || _rewards?.HasChosen != true)
        {
            return false;
        }

        if (_build?.IsOpen == true)
        {
            _build.Close();
        }

        Phase = MapCompletePhase.RouteChoice;
        EmitSignal(SignalName.PhaseChanged, (int)Phase);
        return true;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsBuildManagement)
        {
            return;
        }

        if (@event.IsActionPressed("complete_build", true))
        {
            if (TryCompleteBuild())
            {
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (@event.IsActionPressed("stash_transfer", true))
        {
            if (TryTransferSelected())
            {
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (@event.IsActionPressed("reforge_item", true))
        {
            if (TryReforgeSelected(out _, out _))
            {
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (@event.IsActionPressed("attach_support", true))
        {
            if (_player?.Skills?.TryAttachSupport(SkillSlot.Primary, "volley") == true)
            {
                NotifyBuildDataChanged();
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (@event.IsActionPressed("detach_support", true))
        {
            if (_player?.Skills?.TryDetachSupport(SkillSlot.Primary) == true)
            {
                NotifyBuildDataChanged();
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (@event.IsActionPressed("unequip_item", true))
        {
            if (TryUnequipFirstOccupiedSlot())
            {
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public bool TryMoveInventoryToStash(string itemId, string tabId = "default")
    {
        var moved = IsBuildManagement
            && _player != null
            && _transactions.TryMoveInventoryToStash(
                _player.Inventory,
                _player.Equipment,
                Stash,
                tabId,
                itemId);
        if (moved)
        {
            _selectedStashItemId = itemId;
            NotifyBuildDataChanged();
        }

        return moved;
    }

    public bool TryMoveStashToInventory(string itemId, string tabId = "default")
    {
        var moved = IsBuildManagement
            && _player != null
            && _transactions.TryMoveStashToInventory(
                _player.Inventory,
                _player.Equipment,
                Stash,
                tabId,
                itemId);
        if (moved)
        {
            _selectedStashItemId = string.Empty;
            _build?.SelectItem(itemId);
            NotifyBuildDataChanged();
        }

        return moved;
    }

    public bool TryReforge(string itemId, out CraftingResult? result, out string error)
    {
        result = null;
        error = string.Empty;
        if (!IsBuildManagement || _player == null || _run == null)
        {
            error = "crafting is only available during build management";
            return false;
        }

        var item = _player.Items.FirstOrDefault(candidate => candidate.Id == itemId)
            ?? Stash.Items.FirstOrDefault(candidate => candidate.Id == itemId);
        if (item == null)
        {
            error = "crafting item was not found";
            return false;
        }

        var crafted = _transactions.TryReforge(
            new CraftingRecipe
            {
                Id = "reforge_item",
                Name = "Reforge Item",
                Slot = item.Slot,
                RequiredBaseId = item.BaseId,
                ForgeFragmentCost = 1,
            },
            itemId,
            _player.Inventory,
            _player.Equipment,
            Stash,
            Currency,
            _run.Session,
            out result,
            out error);
        if (crafted && result != null)
        {
            var craftedItem = result.CraftedItem;
            if (_player.Items.Any(candidate => candidate.Id == craftedItem.Id))
            {
                _selectedStashItemId = string.Empty;
                _build.SelectItem(craftedItem.Id);
            }
            else
            {
                _selectedStashItemId = craftedItem.Id;
            }

            NotifyBuildDataChanged();
        }

        return crafted;
    }

    public bool TryTransferSelected()
    {
        if (!IsBuildManagement || _player == null)
        {
            return false;
        }

        var inventoryId = _build?.SelectedItemId;
        if (!string.IsNullOrWhiteSpace(inventoryId)
            && _player.Items.Any(item => item.Id == inventoryId))
        {
            return TryMoveInventoryToStash(inventoryId);
        }

        var stashId = !string.IsNullOrWhiteSpace(_selectedStashItemId)
            && Stash.ContainsItemId(_selectedStashItemId)
                ? _selectedStashItemId
                : Stash.Items.FirstOrDefault()?.Id;
        return !string.IsNullOrWhiteSpace(stashId)
            && TryMoveStashToInventory(stashId);
    }

    public bool TryReforgeSelected(out CraftingResult? result, out string error)
    {
        result = null;
        error = string.Empty;
        if (!IsBuildManagement || _player == null)
        {
            error = "crafting is only available during build management";
            return false;
        }

        var selectedId = _build?.SelectedItemId;
        if (string.IsNullOrWhiteSpace(selectedId)
            || !_player.Items.Any(item => item.Id == selectedId))
        {
            selectedId = _selectedStashItemId;
        }

        if (string.IsNullOrWhiteSpace(selectedId))
        {
            selectedId = Stash.Items.FirstOrDefault()?.Id;
        }

        return !string.IsNullOrWhiteSpace(selectedId)
            && TryReforge(selectedId, out result, out error);
    }

    private bool TryUnequipFirstOccupiedSlot()
    {
        if (_player == null)
        {
            return false;
        }

        foreach (var slot in Enum.GetValues<EquipmentSlot>())
        {
            if (_player.Equipment.ItemInSlot(slot) != null)
            {
                return _player.TryUnequip(slot);
            }
        }

        return false;
    }

    private void NotifyBuildDataChanged() => EmitSignal(SignalName.BuildDataChanged);

    public bool CanRestore(
        IReadOnlyList<Item> stashItems,
        int forgeFragments,
        MapCompletePhase phase)
    {
        return CanRestore(
            stashItems,
            forgeFragments,
            phase,
            _player?.Items ?? Array.Empty<Item>(),
            _player?.EquippedItems ?? new Dictionary<EquipmentSlot, Item>());
    }

    public bool CanRestore(
        IReadOnlyList<Item> stashItems,
        int forgeFragments,
        MapCompletePhase phase,
        IReadOnlyList<Item> targetInventory,
        IReadOnlyDictionary<EquipmentSlot, Item> targetEquipment)
    {
        if (stashItems == null
            || stashItems.Count > 24
            || forgeFragments < 0
            || !Enum.IsDefined(phase)
            || targetInventory == null
            || targetEquipment == null)
        {
            return false;
        }

        var occupiedIds = new HashSet<string>(
            targetInventory.Where(item => item != null).Select(item => item.Id),
            StringComparer.Ordinal);
        foreach (var pair in targetEquipment)
        {
            if (pair.Value == null || !occupiedIds.Add(pair.Value.Id))
            {
                return false;
            }
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in stashItems)
        {
            if (item == null || !ids.Add(item.Id))
            {
                return false;
            }

            try
            {
                item.Validate();
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (occupiedIds.Contains(item.Id))
            {
                return false;
            }
        }

        return true;
    }

    public bool RestoreState(
        IReadOnlyList<Item> stashItems,
        int forgeFragments,
        MapCompletePhase phase)
    {
        if (!CanRestore(stashItems, forgeFragments, phase))
        {
            return false;
        }

        Stash.RestoreItems(stashItems);
        Currency.Restore(forgeFragments);
        Phase = phase;
        _selectedStashItemId = Stash.Items.FirstOrDefault()?.Id ?? string.Empty;
        EmitSignal(SignalName.PhaseChanged, (int)Phase);
        NotifyBuildDataChanged();
        if (Phase == MapCompletePhase.BuildManagement)
        {
            _build?.Open();
        }

        return true;
    }

    private void BindRuntime()
    {
        if (_exiting || !IsInsideTree())
        {
            return;
        }

        _player = GetNodeOrNull<PlayerController3D>("../Player3D");
        _build = _player?.GetNodeOrNull<PlayerBuildController3D>("PlayerBuildController3D") ?? null!;
        _rewards = GetNodeOrNull<MapRewardNode3D>("../MapRewards3D");
        _flow = GetNodeOrNull<GameFlowController3D>("../GameFlow3D");
        _run = MapRuntimeScope3D.FindRunSession(this);
        if (_player == null || _rewards == null || _flow == null || _run == null)
        {
            CallDeferred(nameof(BindRuntime));
            return;
        }

        _rewards.RewardChosen += OnRewardChosen;
    }

    private void OnRewardChosen(string rewardId)
    {
        if (_flow?.State != GameFlowState.MapComplete)
        {
            return;
        }

        // Save recovery restores the phase before replaying the reward signal.
        // A persisted RouteChoice must not regress to BuildManagement merely
        // because MapRewardNode3D re-emits the already chosen reward.
        if (Phase == MapCompletePhase.RouteChoice)
        {
            return;
        }

        Phase = MapCompletePhase.BuildManagement;
        EmitSignal(SignalName.PhaseChanged, (int)Phase);
        _build?.Open();
    }
}
