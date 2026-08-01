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

    public MapCompletePhase Phase { get; private set; } = MapCompletePhase.RewardChoice;
    public Stash Stash { get; } = Stash.CreateDefault();
    public RunCurrencyWallet Currency => _build?.Currency ?? _fallbackCurrency;
    public IReadOnlyList<Item> StashItems => Stash.Items;
    public bool IsBuildManagement => Phase == MapCompletePhase.BuildManagement;
    public bool IsRouteChoice => Phase == MapCompletePhase.RouteChoice;

    private readonly RunCurrencyWallet _fallbackCurrency = new();
    private readonly BuildcraftTransactions _transactions = new();
    private PlayerController3D _player = null!;
    private PlayerBuildController3D _build = null!;
    private MapRewardNode3D _rewards = null!;
    private GameFlowController3D _flow = null!;
    private RunSessionNode _run = null!;
    private bool _exiting;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
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

    public bool TryMoveInventoryToStash(string itemId, string tabId = "default") =>
        IsBuildManagement
        && _player != null
        && _transactions.TryMoveInventoryToStash(
            _player.Inventory,
            _player.Equipment,
            Stash,
            tabId,
            itemId);

    public bool TryMoveStashToInventory(string itemId, string tabId = "default") =>
        IsBuildManagement
        && _player != null
        && _transactions.TryMoveStashToInventory(
            _player.Inventory,
            _player.Equipment,
            Stash,
            tabId,
            itemId);

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

        return _transactions.TryReforge(
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
    }

    public bool CanRestore(
        IReadOnlyList<Item> stashItems,
        int forgeFragments,
        MapCompletePhase phase)
    {
        if (stashItems == null || stashItems.Count > 24 || forgeFragments < 0 || !Enum.IsDefined(phase))
        {
            return false;
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

            if (_player != null
                && (_player.Inventory.Contains(item.Id) || _player.Equipment.ContainsItemId(item.Id)))
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
        EmitSignal(SignalName.PhaseChanged, (int)Phase);
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
