using System;
using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Verifies the formal 3D Atlas route boundary: map completion unlocks routes,
/// reward choice precedes route choice, N cannot skip an unselected route, and
/// the selected route drives the next map plan.
/// </summary>
public partial class AtlasRouteChoice3DRegressionSmoke : Node
{
    [Export] public bool SaveRecoveryMode { get; set; }

    private RunSessionNode _run;
    private TestArena3D _firstArena;
    private TestArena3D _secondArena;
    private GameFlowController3D _flow;
    private MapRewardNode3D _rewards;
    private AtlasRouteChoiceController3D _route;
    private EncounterDirector3D _director;
    private CombatHudController3D _hud;
    private readonly Dictionary<int, int> _waveStarted = new();
    private readonly Dictionary<int, int> _waveCleared = new();
    private double _elapsed;
    private double _transitionElapsed;
    private bool _bound;
    private bool _routeRequested;
    private bool _complete;
    private AtlasRouteSaveRecoveryRunner _saveRecoveryRunner;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        if (SaveRecoveryMode)
        {
            _saveRecoveryRunner = new AtlasRouteSaveRecoveryRunner();
            _saveRecoveryRunner.Ready(this);
        }
    }

    public override void _Process(double delta)
    {
        if (SaveRecoveryMode)
        {
            _saveRecoveryRunner?.Tick(delta);
            return;
        }

        if (_complete)
        {
            return;
        }

        _elapsed += delta;
        if (!_bound)
        {
            BindFirstMap();
            if (!_bound)
            {
                if (_elapsed > 12.0)
                {
                    Fail("Atlas route smoke nodes did not become ready");
                }

                return;
            }
        }

        if (!_routeRequested)
        {
            TickFirstMap();
            return;
        }

        TickSecondMap(delta);
    }

    private void BindFirstMap()
    {
        _run ??= GetNodeOrNull<RunSessionNode>("RunShell3D");
        _firstArena ??= GetTree().GetNodesInGroup("arena_3d")
            .OfType<TestArena3D>()
            .LastOrDefault(map => map.UsesEncounterRuntime);
        if (_firstArena == null)
        {
            return;
        }

        _flow = _firstArena.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        _rewards = _firstArena.GetNodeOrNull<MapRewardNode3D>("MapRewards3D");
        _route = _firstArena.GetNodeOrNull<AtlasRouteChoiceController3D>("AtlasRouteChoice3D");
        _director = _firstArena.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        _hud = _firstArena.GetNodeOrNull<CombatHudController3D>("HUD");
        if (_run == null || _flow == null || _rewards == null || _route == null
            || _director == null || _hud == null || _run.Atlas == null)
        {
            return;
        }

        if (_run.CurrentAtlasMapId != "quiet-coast"
            || _run.Atlas.State.IsCompleted("quiet-coast")
            || _run.PendingAtlasMapId != string.Empty
            || !_run.CurrentMapPlan.AtlasMapId.Equals("quiet-coast", StringComparison.Ordinal))
        {
            Fail("formal Atlas did not start at an incomplete quiet-coast node");
            return;
        }

        _director.WaveStarted += OnWaveStarted;
        _director.WaveCleared += OnWaveCleared;
        _bound = true;
    }

    private void TickFirstMap()
    {
        if (_flow.State == GameFlowState.Playing)
        {
            KillActiveEnemies();
            return;
        }

        if (_flow.State != GameFlowState.MapComplete)
        {
            Fail($"first route map ended in unexpected state {_flow.State}");
            return;
        }

        if (_run.AtlasCompletionCount != 1
            || !_run.Atlas.State.IsCompleted("quiet-coast")
            || !_run.Atlas.State.IsUnlocked("hardened-frontier")
            || !_run.Atlas.State.IsUnlocked("volatile-rift")
            || _director.EncounterCompletedCount != 1
            || _director.ActiveEnemyCount != 0
            || !EachWaveExactlyOnce(_waveStarted)
            || !EachWaveExactlyOnce(_waveCleared))
        {
            Fail("quiet-coast encounter or Atlas completion contract was not exact");
            return;
        }

        if (_route.TrySelect(0))
        {
            Fail("route selection was accepted before reward choice");
            return;
        }

        if (!_rewards.TryChooseReward(0)
            || !_route.ChoiceActive
            || _route.OptionCount != 2
            || _route.TryConfirm())
        {
            Fail("reward-to-route ordering or unselected N guard was invalid");
            return;
        }

        if (!_route.TrySelect(1)
            || _route.SelectedMapId != "volatile-rift"
            || _run.PendingAtlasMapId != "volatile-rift")
        {
            Fail("volatile-rift route selection was rejected");
            return;
        }

        _routeRequested = true;
        if (!_route.TryConfirm())
        {
            Fail("selected Atlas route could not be confirmed");
        }
    }

    private void TickSecondMap(double delta)
    {
        _transitionElapsed += delta;
        _secondArena ??= GetTree().GetNodesInGroup("arena_3d")
            .OfType<TestArena3D>()
            .LastOrDefault(map => map.UsesEncounterRuntime
                && !ReferenceEquals(map, _firstArena));
        if (_run.CurrentMapLevel != 2 || _secondArena == null)
        {
            if (_transitionElapsed > 12.0)
            {
                Fail("selected route did not create map 2");
            }

            return;
        }

        if (GodotObject.IsInstanceValid(_firstArena))
        {
            if (_transitionElapsed > 4.0)
            {
                Fail("old Atlas route map was not released");
            }

            return;
        }

        var plan = _secondArena.AppliedRunPlan;
        var secondHud = _secondArena.GetNodeOrNull<CombatHudController3D>("HUD");
        var expectedModifier = _run.CurrentMapModifierId == "volatile-hunt";
        var expectedEncounter = _run.CurrentEncounterId == "crossfire_advance";
        if (!expectedModifier
            || !expectedEncounter
            || plan?.AtlasMapId != "volatile-rift"
            || plan?.Modifier.Id != "volatile-hunt"
            || plan?.EncounterId != "crossfire_advance"
            || plan?.DropItemLevel != 3
            || _run.RouteSelectionCount != 1
            || _route.ConfirmCount != 1
            || secondHud == null
            || secondHud.EncounterId != "crossfire_advance"
            || !secondHud.EncounterText.Contains("Crossfire Advance"))
        {
            Fail($"selected route plan/HUD mismatch map={_run.CurrentAtlasMapId} modifier={_run.CurrentMapModifierId} encounter={_run.CurrentEncounterId} planAtlas={plan?.AtlasMapId} planModifier={plan?.Modifier.Id} planEncounter={plan?.EncounterId} drop={plan?.DropItemLevel} routeCount={_run.RouteSelectionCount}/{_route.ConfirmCount} hud={secondHud?.EncounterId}");
            return;
        }

        _complete = true;
        GD.Print("ATLAS_ROUTE_CHOICE_3D_REGRESSION_PASS completion=true ordering=true options=2 unselected_n_blocked=true volatile_route=true plan=true hud=true old_map_released=true");
        // Flush Godot-managed arrays before the process exits; the managed wrapper
        // finalizer must run while the native Godot runtime is still alive.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GetTree().Quit();
    }

    private void KillActiveEnemies()
    {
        var request = new DamageRequest(
            999999,
            DamageType.Physical,
            "atlas_route_smoke",
            CombatFaction.Player);
        foreach (var enemy in _director.GetActiveEnemies().ToArray())
        {
            switch (enemy)
            {
                case FeralController3D feral:
                    feral.ApplyDamage(request);
                    break;
                case SpitterController3D spitter:
                    spitter.ApplyDamage(request);
                    break;
                case BrimstoneColossusController3D boss:
                    boss.ApplyDamage(request);
                    break;
            }
        }
    }

    private bool EachWaveExactlyOnce(IReadOnlyDictionary<int, int> counts)
    {
        return counts.Count == _director.TotalWaveCount
            && counts.Values.All(count => count == 1);
    }

    private void OnWaveStarted(int waveIndex, string waveId)
    {
        _waveStarted[waveIndex] = _waveStarted.GetValueOrDefault(waveIndex) + 1;
    }

    private void OnWaveCleared(int waveIndex, string waveId)
    {
        _waveCleared[waveIndex] = _waveCleared.GetValueOrDefault(waveIndex) + 1;
    }

    private void Fail(string reason)
    {
        _complete = true;
        GD.PushError($"ATLAS_ROUTE_CHOICE_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}

public partial class AtlasRouteChoice3DRegressionSmoke
{
    private sealed class AtlasRouteSaveRecoveryRunner
    {
    private Node _owner;

    private enum Stage
    {
        PlayingSave,
        ClearingMap,
        MapCompleteBeforeReward,
        MapCompleteAfterReward,
        MapCompletePendingRoute,
        WaitingForNextMap,
        NextMapSave,
    }

    private Stage _stage = Stage.PlayingSave;
    private RunSessionNode _run;
    private TestArena3D _firstArena;
    private TestArena3D _secondArena;
    private PlayerController3D _player;
    private GameFlowController3D _flow;
    private MapRewardNode3D _rewards;
    private AtlasRouteChoiceController3D _route;
    private EncounterDirector3D _director;
    private SaveBoundaryNode3D _save;
    private int _baselineModifierResolveCount;
    private int _baselineEncounterResolveCount;
    private ulong _baselineLootState;
    private ulong _baselineCraftingState;
    private ulong _baselineEventState;
    private int _baselineItemSequence;
    private int _baselineRouteSelectionCount;
    private string[] _baselineUnlockedMapIds = Array.Empty<string>();
    private string[] _baselineCompletedMapIds = Array.Empty<string>();
    private Stats _baselineRewardStats;
    private string _expectedPendingId = string.Empty;
    private double _elapsed;
    private double _stageElapsed;
    private double _transitionElapsed;
    private bool _bound;
    private bool _complete;

    public void Ready(Node owner)
    {
        _owner = owner;
        _owner.ProcessMode = Node.ProcessModeEnum.Always;
        new MinimalSaveService().Delete();
    }

    public void Tick(double delta)
    {
        if (_complete)
        {
            return;
        }

        _elapsed += delta;
        _stageElapsed += delta;
        if (_stage < Stage.WaitingForNextMap)
        {
            BindFirstMap();
        }
        if (!_bound)
        {
            if (_elapsed > 12.0)
            {
                Fail("Atlas save smoke nodes did not become ready");
            }

            return;
        }

        switch (_stage)
        {
            case Stage.PlayingSave:
                TickPlayingSave();
                break;
            case Stage.ClearingMap:
                TickClearingMap();
                break;
            case Stage.MapCompleteBeforeReward:
                TickBeforeReward();
                break;
            case Stage.MapCompleteAfterReward:
                TickAfterReward();
                break;
            case Stage.MapCompletePendingRoute:
                TickPendingRoute();
                break;
            case Stage.WaitingForNextMap:
                TickNextMap(delta);
                break;
            case Stage.NextMapSave:
                TickNextMapSave();
                break;
        }

        if (_stageElapsed > 35.0 && !_complete)
        {
            Fail($"stage {_stage} timed out map={_run.CurrentAtlasMapId} pending={_run.PendingAtlasMapId}");
        }
    }

    private void BindFirstMap()
    {
        _run ??= _owner.GetNodeOrNull<RunSessionNode>("RunShell3D");
        _firstArena ??= _owner.GetTree().GetNodesInGroup("arena_3d")
            .OfType<TestArena3D>()
            .LastOrDefault(map => map.UsesEncounterRuntime);
        if (_firstArena == null)
        {
            return;
        }

        _player = _firstArena.GetNodeOrNull<PlayerController3D>("Player3D");
        _flow = _firstArena.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        _rewards = _firstArena.GetNodeOrNull<MapRewardNode3D>("MapRewards3D");
        _route = _firstArena.GetNodeOrNull<AtlasRouteChoiceController3D>("AtlasRouteChoice3D");
        _director = _firstArena.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        _save = _firstArena.GetNodeOrNull<SaveBoundaryNode3D>("SaveBoundary3D");
        _bound = _run != null && _player != null && _flow != null && _rewards != null
            && _route != null && _director != null && _save != null && _run.Atlas != null;
    }

    private void TickPlayingSave()
    {
        if (_flow.State != GameFlowState.Playing)
        {
            Fail("run was not Playing at the first save boundary");
            return;
        }

        CaptureBaseline();
        if (!_save.TrySaveCurrentRun(out var saveError))
        {
            Fail($"Playing save failed: {saveError}");
            return;
        }

        if (!RestoreAndVerify(
                "Playing save/restore",
                GameFlowState.Playing,
                rewardChosen: false,
                currentId: "quiet-coast",
                pendingId: string.Empty))
        {
            return;
        }

        _stage = Stage.ClearingMap;
        _stageElapsed = 0.0;
    }

    private void TickClearingMap()
    {
        if (_flow.State == GameFlowState.Playing)
        {
            KillActiveEnemies();
            return;
        }

        if (_flow.State != GameFlowState.MapComplete)
        {
            Fail($"map completion did not enter MapComplete: {_flow.State}");
            return;
        }

        _stage = Stage.MapCompleteBeforeReward;
        _stageElapsed = 0.0;
    }

    private void TickBeforeReward()
    {
        if (_rewards.HasChosen || _route.ChoiceActive)
        {
            Fail("reward or route became active before the reward choice");
            return;
        }

        CaptureBaseline();
        if (!_save.TrySaveCurrentRun(out var saveError)
            || !RestoreAndVerify(
                "MapComplete before reward",
                GameFlowState.MapComplete,
                rewardChosen: false,
                currentId: "quiet-coast",
                pendingId: string.Empty))
        {
            Fail($"MapComplete-before-reward save failed: {saveError}");
            return;
        }

        if (!_rewards.TryChooseReward(0))
        {
            Fail("reward selection failed after recovery");
            return;
        }

        _stage = Stage.MapCompleteAfterReward;
        _stageElapsed = 0.0;
    }

    private void TickAfterReward()
    {
        if (!_rewards.HasChosen || !_route.ChoiceActive || _run.PendingAtlasMapId != string.Empty)
        {
            Fail("reward recovery did not expose the route choice");
            return;
        }

        CaptureBaseline();
        if (!_save.TrySaveCurrentRun(out var saveError)
            || !RestoreAndVerify(
                "MapComplete after reward",
                GameFlowState.MapComplete,
                rewardChosen: true,
                currentId: "quiet-coast",
                pendingId: string.Empty))
        {
            Fail($"MapComplete-after-reward save failed: {saveError}");
            return;
        }

        if (!_route.TrySelect(0) || _run.PendingAtlasMapId != "hardened-frontier")
        {
            Fail("hardened-frontier route selection failed");
            return;
        }

        _expectedPendingId = "hardened-frontier";
        _stage = Stage.MapCompletePendingRoute;
        _stageElapsed = 0.0;
    }

    private void TickPendingRoute()
    {
        if (_run.PendingAtlasMapId != _expectedPendingId || !_route.ChoiceActive)
        {
            Fail("pending route was not visible before its save");
            return;
        }

        CaptureBaseline();
        if (!_save.TrySaveCurrentRun(out var saveError)
            || !RestoreAndVerify(
                "MapComplete pending route",
                GameFlowState.MapComplete,
                rewardChosen: true,
                currentId: "quiet-coast",
                pendingId: _expectedPendingId))
        {
            Fail($"pending-route save failed: {saveError}");
            return;
        }

        if (!_route.TryConfirm())
        {
            Fail("pending route could not be confirmed after recovery");
            return;
        }

        _stage = Stage.WaitingForNextMap;
        _stageElapsed = 0.0;
        _transitionElapsed = 0.0;
    }

    private void TickNextMap(double delta)
    {
        _transitionElapsed += delta;
        _secondArena ??= _owner.GetTree().GetNodesInGroup("arena_3d")
            .OfType<TestArena3D>()
            .LastOrDefault(map => map.UsesEncounterRuntime
                && !ReferenceEquals(map, _firstArena));
        if (_secondArena == null || _run.CurrentMapLevel != 2)
        {
            if (_transitionElapsed > 12.0)
            {
                Fail("selected map did not become ready after recovery");
            }

            return;
        }

        if (GodotObject.IsInstanceValid(_firstArena))
        {
            if (_transitionElapsed > 4.0)
            {
                Fail("old map remained valid after recovered route transition");
            }

            return;
        }

        if (_run.CurrentAtlasMapId != "hardened-frontier"
            || _run.PendingAtlasMapId != string.Empty
            || _run.CurrentMapModifierId != "hardened-front"
            || _run.CurrentEncounterId != "crossfire_advance"
            || _secondArena.AppliedRunPlan?.AtlasMapId != "hardened-frontier"
            || _secondArena.AppliedRunPlan?.DropItemLevel != 3)
        {
            Fail("selected route state did not survive new-map creation");
            return;
        }

        _player = _secondArena.GetNodeOrNull<PlayerController3D>("Player3D");
        _flow = _secondArena.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        _rewards = _secondArena.GetNodeOrNull<MapRewardNode3D>("MapRewards3D");
        _route = _secondArena.GetNodeOrNull<AtlasRouteChoiceController3D>("AtlasRouteChoice3D");
        _director = _secondArena.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        _save = _secondArena.GetNodeOrNull<SaveBoundaryNode3D>("SaveBoundary3D");
        _stage = Stage.NextMapSave;
        _stageElapsed = 0.0;
    }

    private void TickNextMapSave()
    {
        CaptureBaseline();
        if (!_save.TrySaveCurrentRun(out var saveError)
            || !RestoreAndVerify(
                "new map Playing",
                GameFlowState.Playing,
                rewardChosen: false,
                currentId: "hardened-frontier",
                pendingId: string.Empty))
        {
            Fail($"new-map save failed: {saveError}");
            return;
        }

        _complete = true;
        GD.Print("ATLAS_ROUTE_SAVE_RECOVERY_3D_REGRESSION_PASS playing=true before_reward=true after_reward=true pending_route=true new_map=true atlas=true route_count=true rng=true plan=true");
        // Flush Godot-managed arrays before the process exits; the managed wrapper
        // finalizer must run while the native Godot runtime is still alive.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        _owner.GetTree().Quit();
    }

    private bool RestoreAndVerify(
        string operation,
        GameFlowState expectedFlowState,
        bool rewardChosen,
        string currentId,
        string pendingId)
    {
        if (!_save.TryLoadAndApplyLastRun(out _, out var restoreError))
        {
            Fail($"{operation} restore failed: {restoreError}");
            return false;
        }

        if (_flow.State != expectedFlowState
            || _rewards.HasChosen != rewardChosen
            || _run.PendingAtlasMapId != pendingId
            || _run.CurrentAtlasMapId != currentId
            || !_run.Atlas.State.UnlockedMapIds.OrderBy(id => id)
                .SequenceEqual(_baselineUnlockedMapIds.OrderBy(id => id))
            || !_run.Atlas.State.CompletedMapIds.OrderBy(id => id)
                .SequenceEqual(_baselineCompletedMapIds.OrderBy(id => id))
            || _run.RouteSelectionCount != _baselineRouteSelectionCount)
        {
            Fail($"{operation} changed Atlas/reward state unexpectedly");
            return false;
        }

        if (_run.MapModifierResolveCount != _baselineModifierResolveCount
            || _run.EncounterResolveCount != _baselineEncounterResolveCount
            || _run.Session.LootRandom.State != _baselineLootState
            || _run.Session.CraftingRandom.State != _baselineCraftingState
            || _run.Session.EventRandom.State != _baselineEventState
            || _run.Session.ItemSequence != _baselineItemSequence
            || !_player.RewardStats.EquivalentTo(_baselineRewardStats))
        {
            Fail($"{operation} rerolled plan, RNG, identity, or reward stats");
            return false;
        }

        return true;
    }

    private void CaptureBaseline()
    {
        _baselineModifierResolveCount = _run.MapModifierResolveCount;
        _baselineEncounterResolveCount = _run.EncounterResolveCount;
        _baselineLootState = _run.Session.LootRandom.State;
        _baselineCraftingState = _run.Session.CraftingRandom.State;
        _baselineEventState = _run.Session.EventRandom.State;
        _baselineItemSequence = _run.Session.ItemSequence;
        _baselineRewardStats = _player.RewardStats;
        _baselineRouteSelectionCount = _run.RouteSelectionCount;
        _baselineUnlockedMapIds = _run.Atlas.State.UnlockedMapIds.ToArray();
        _baselineCompletedMapIds = _run.Atlas.State.CompletedMapIds.ToArray();
    }

    private void KillActiveEnemies()
    {
        var request = new DamageRequest(
            999999,
            DamageType.Physical,
            "atlas_save_recovery_smoke",
            CombatFaction.Player);
        foreach (var enemy in _director.GetActiveEnemies().ToArray())
        {
            switch (enemy)
            {
                case FeralController3D feral:
                    feral.ApplyDamage(request);
                    break;
                case SpitterController3D spitter:
                    spitter.ApplyDamage(request);
                    break;
                case BrimstoneColossusController3D boss:
                    boss.ApplyDamage(request);
                    break;
            }
        }
    }

    private void Fail(string reason)
    {
        _complete = true;
        GD.PushError($"ATLAS_ROUTE_SAVE_RECOVERY_3D_REGRESSION_FAIL {reason}");
        _owner.GetTree().Quit(1);
    }
}
}
