using System;
using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// End-to-end run-plan contract: pre-ready encounter binding, real map
/// transition, HUD rebinding, save determinism, pause stability, and the
/// frozen composition of all three encounter resources.
/// </summary>
public partial class EncounterPlanRuntime3DRegressionSmoke : Node
{
    private enum Stage
    {
        MapOne,
        WaitingForMapTwo,
        MapTwo,
        Paused,
        ForcedExecution,
        Complete,
    }

    private Stage _stage = Stage.MapOne;
    private RunSessionNode _run;
    private TestArena3D _arena;
    private TestArena3D _firstArena;
    private PlayerController3D _player;
    private EncounterDirector3D _director;
    private GameFlowController3D _flow;
    private CombatHudController3D _hud;
    private SaveBoundaryNode3D _save;
    private double _elapsed;
    private double _stageElapsed;
    private double _pauseElapsed;
    private string _savedEncounterId;
    private ulong _savedEncounterSeed;
    private ulong _savedModifierSeed;
    private int _savedEncounterResolveCount;
    private int _savedModifierResolveCount;
    private int _savedWaveIndex;
    private int _savedActiveEnemyCount;
    private int _savedSpawnedEnemyCount;
    private ulong _savedLootState;
    private ulong _savedCraftingState;
    private ulong _savedEventState;
    private bool _complete;
    private TestArena3D _forcedArena;
    private EncounterDirector3D _forcedDirector;
    private int _forcedIndex;
    private readonly Dictionary<int, int> _forcedWaveStarted = new();
    private readonly Dictionary<int, int> _forcedWaveCleared = new();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (_complete)
        {
            return;
        }

        _elapsed += delta;
        _stageElapsed += delta;
        BindRuntime();
        if (_run == null || _arena == null || _player == null || _director == null || _flow == null || _hud == null)
        {
            if (_elapsed > 12.0)
            {
                Fail("encounter plan runtime nodes did not become ready");
            }

            return;
        }

        switch (_stage)
        {
            case Stage.MapOne:
                TickMapOne();
                break;
            case Stage.WaitingForMapTwo:
                TickWaitingForMapTwo();
                break;
            case Stage.MapTwo:
                TickMapTwo();
                break;
            case Stage.Paused:
                TickPaused();
                break;
            case Stage.ForcedExecution:
                TickForcedExecution();
                break;
        }

        if (_stageElapsed > 35.0 && _stage != Stage.Complete)
        {
            Fail($"stage {_stage} timed out level={_run.CurrentMapLevel} encounter={_run.CurrentEncounterId}");
        }
    }

    private void BindRuntime()
    {
        _run ??= GetNodeOrNull<RunSessionNode>("RunShell3D");
        if (_arena == null || !GodotObject.IsInstanceValid(_arena))
        {
            _arena = GetTree().GetNodesInGroup("arena_3d")
                .OfType<TestArena3D>()
                .LastOrDefault(map => map.UsesEncounterRuntime
                    && (_firstArena == null || !ReferenceEquals(map, _firstArena)));
            if (_arena == null && _firstArena == null)
            {
                _arena = GetTree().GetNodesInGroup("arena_3d")
                    .OfType<TestArena3D>()
                    .LastOrDefault(map => map.UsesEncounterRuntime);
            }
        }

        if (_arena == null)
        {
            return;
        }

        _player = _arena.GetNodeOrNull<PlayerController3D>("Player3D");
        _director = _arena.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        _flow = _arena.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        _hud = _arena.GetNodeOrNull<CombatHudController3D>("HUD");
        _save = _arena.GetNodeOrNull<SaveBoundaryNode3D>("SaveBoundary3D");
    }

    private void TickMapOne()
    {
        if (_run.CurrentMapLevel != 1 || _arena.RunPlanAppliedCount != 1)
        {
            Fail("Map 1 run plan was not applied exactly once");
            return;
        }

        if (!VerifyPlanAndHud())
        {
            return;
        }

        KillActiveEnemies();
        if (_flow.State != GameFlowState.MapComplete)
        {
            return;
        }

        var oldArena = _arena;
        var reward = _arena.GetNodeOrNull<MapRewardNode3D>("MapRewards3D");
        if (reward == null || !reward.TryChooseReward(0))
        {
            Fail("Map 1 reward could not be selected");
            return;
        }

        _firstArena = oldArena;
        if (!_run.LoadNextMap())
        {
            Fail("Map 1 could not transition to Map 2");
            return;
        }

        _arena = null;
        _director = null;
        _flow = null;
        _hud = null;
        _save = null;
        _stage = Stage.WaitingForMapTwo;
        _stageElapsed = 0.0;
    }

    private void TickWaitingForMapTwo()
    {
        if (_run.CurrentMapLevel != 2
            || _arena == null
            || ReferenceEquals(_arena, _firstArena)
            || _arena.RunPlanAppliedCount != 1)
        {
            return;
        }

        if (!VerifyPlanAndHud())
        {
            return;
        }

        CaptureDeterministicState();
        var saveError = string.Empty;
        if (_save == null || !_save.TrySaveCurrentRun(out saveError))
        {
            Fail($"Map 2 plan save failed: {saveError}");
            return;
        }

        if (!_save.TryLoadAndApplyLastRun(out _, out var restoreError))
        {
            Fail($"Map 2 plan restore failed: {restoreError}");
            return;
        }

        if (!VerifyDeterministicState("save/restore"))
        {
            return;
        }

        _savedWaveIndex = _director.CurrentWaveIndex;
        _savedActiveEnemyCount = _director.ActiveEnemyCount;
        _savedSpawnedEnemyCount = _director.SpawnedEnemyCount;
        _pauseElapsed = 0.0;
        _flow.RestoreState(GameFlowState.GameOver);
        _stage = Stage.Paused;
        _stageElapsed = 0.0;
    }

    private void TickMapTwo()
    {
        // The runtime contract is complete before a second map is cleared;
        // this state remains available for a future full progression smoke.
        _stage = Stage.Complete;
    }

    private void TickPaused()
    {
        if (_pauseElapsed < 1.0)
        {
            _pauseElapsed += 1.0 / 60.0;
            return;
        }

        if (_director.CurrentWaveIndex != _savedWaveIndex
            || _director.ActiveEnemyCount != _savedActiveEnemyCount
            || _director.SpawnedEnemyCount != _savedSpawnedEnemyCount
            || _run.CurrentEncounterSeed != _savedEncounterSeed
            || _run.EncounterResolveCount != _savedEncounterResolveCount)
        {
            Fail("GameOver pause changed encounter plan or director progression");
            return;
        }

        _flow.RestoreState(GameFlowState.Playing);
        _director.ProcessMode = ProcessModeEnum.Disabled;
        _director.Enabled = false;
        _forcedIndex = 0;
        _stage = Stage.ForcedExecution;
        _stageElapsed = 0.0;
        BeginForcedEncounter();
    }

    private void BeginForcedEncounter()
    {
        if (_forcedIndex >= 3)
        {
            _stage = Stage.Complete;
            _complete = true;
            GD.Print("ENCOUNTER_PLAN_RUNTIME_3D_REGRESSION_PASS pre_ready=true map_transition=true hud=true save_stable=true pause_stable=true forced_composition=true");
            // Flush Godot-managed arrays before the process exits; the managed wrapper
            // finalizer must run while the native Godot runtime is still alive.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GetTree().Quit();
            return;
        }

        var paths = new[]
        {
            "res://resources/encounters/QuietCoastSkirmish3D.tres",
            "res://resources/encounters/CrossfireAdvance3D.tres",
            "res://resources/encounters/SiegePressure3D.tres",
        };
        var tiers = new[] { 1, 2, 3 };
        var definition = GD.Load<EncounterDefinitionResource3D>(paths[_forcedIndex]);
        var arenaScene = GD.Load<PackedScene>("res://scenes3d/TestArena3D.tscn");
        if (definition == null || arenaScene == null)
        {
            Fail($"forced encounter resources could not load index={_forcedIndex}");
            return;
        }

        _forcedArena = arenaScene.Instantiate<TestArena3D>();
        var encounterSeed = RandomService.DeriveSeed(
            _run.CurrentEncounterSeed,
            (ulong)(_forcedIndex + 11));
        var forcedPlan = new RunMapPlan3D(
            _run,
            _run.CurrentMapLevel,
            _run.CurrentMapModifier,
            _run.CurrentMapModifierSeed,
            definition,
            definition.EncounterId,
            definition.EncounterId,
            tiers[_forcedIndex],
            encounterSeed,
            string.Empty,
            string.Empty,
            0,
            Math.Max(1, MapScaling.ItemLevel(
                _run.CurrentMapLevel,
                _run.CurrentMapModifier?.Effects ?? new MapModifierStats())));
        try
        {
            _forcedArena.ConfigureBeforeReady(forcedPlan);
            _run.AddChild(_forcedArena);
        }
        catch (Exception exception)
        {
            Fail($"forced encounter setup failed: {exception.Message}");
            return;
        }

        _forcedDirector = _forcedArena.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        var forcedFlow = _forcedArena.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        if (_forcedDirector == null || forcedFlow == null)
        {
            Fail("forced encounter arena was missing director or flow");
            return;
        }

        forcedFlow.ProcessMode = ProcessModeEnum.Disabled;
        _forcedDirector.ProcessMode = ProcessModeEnum.Always;
        _forcedDirector.SetPhysicsProcess(true);
        _forcedDirector.WaveStarted += OnForcedWaveStarted;
        _forcedDirector.WaveCleared += OnForcedWaveCleared;
        _forcedWaveStarted.Clear();
        _forcedWaveCleared.Clear();
    }

    private void TickForcedExecution()
    {
        if (_forcedDirector == null || !GodotObject.IsInstanceValid(_forcedDirector))
        {
            Fail("forced encounter director disappeared");
            return;
        }

        KillForcedActiveEnemies();
        if (!_forcedDirector.IsEncounterComplete())
        {
            return;
        }

        var definition = _forcedDirector.DefinitionResource;
        var exactSignals = _forcedWaveStarted.Count == definition.Waves.Count
            && _forcedWaveCleared.Count == definition.Waves.Count
            && _forcedWaveStarted.Values.All(value => value == 1)
            && _forcedWaveCleared.Values.All(value => value == 1);
        if (!exactSignals
            || _forcedDirector.ActiveEnemyCount != 0
            || _forcedDirector.EncounterCompletedCount != 1
            || _forcedDirector.SpawnedEnemyCount != definition.Waves.Sum(wave => wave.TotalSpawnCount)
            || _forcedDirector.SpawnedBossCount != 1
            || !definition.Waves.Last().Entries.Any(entry => entry.EnemyScene.ResourcePath.Contains("Brimstone")))
        {
            Fail($"forced encounter execution was not exact id={definition.EncounterId} started={_forcedWaveStarted.Count} cleared={_forcedWaveCleared.Count} spawned={_forcedDirector.SpawnedEnemyCount} active={_forcedDirector.ActiveEnemyCount}");
            return;
        }

        _forcedDirector.WaveStarted -= OnForcedWaveStarted;
        _forcedDirector.WaveCleared -= OnForcedWaveCleared;
        _forcedDirector.ProcessMode = ProcessModeEnum.Disabled;
        _forcedArena.QueueFree();
        _forcedArena = null;
        _forcedDirector = null;
        _forcedIndex++;
        CallDeferred(nameof(BeginForcedEncounter));
    }

    private void KillForcedActiveEnemies()
    {
        var request = new DamageRequest(
            999999,
            DamageType.Physical,
            "encounter_plan_forced_smoke",
            CombatFaction.Player);
        foreach (var enemy in _forcedDirector.GetActiveEnemies().ToArray())
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

    private void OnForcedWaveStarted(int waveIndex, string waveId)
    {
        _forcedWaveStarted[waveIndex] = _forcedWaveStarted.GetValueOrDefault(waveIndex) + 1;
    }

    private void OnForcedWaveCleared(int waveIndex, string waveId)
    {
        _forcedWaveCleared[waveIndex] = _forcedWaveCleared.GetValueOrDefault(waveIndex) + 1;
    }

    private bool VerifyPlanAndHud()
    {
        if (_run.CurrentEncounterDefinition == null
            || !ReferenceEquals(_director.DefinitionResource, _run.CurrentEncounterDefinition)
            || _director.CurrentEncounterId != _run.CurrentEncounterId
            || _run.CurrentMapModifierId != _arena.AppliedRunPlan?.Modifier.Id
            || _arena.AppliedRunPlan?.EncounterId != _run.CurrentEncounterId
            || _hud.EncounterId != _run.CurrentEncounterId
            || !_hud.EncounterText.Contains(_run.CurrentEncounterDisplayName)
            || !_hud.EncounterText.Contains($"Tier {_run.CurrentEncounterTier}"))
        {
            Fail($"run plan/HUD mismatch level={_run.CurrentMapLevel} session={_run.CurrentEncounterId} director={_director.CurrentEncounterId} hud={_hud.EncounterId}");
            return false;
        }

        return true;
    }

    private void CaptureDeterministicState()
    {
        _savedEncounterId = _run.CurrentEncounterId;
        _savedEncounterSeed = _run.CurrentEncounterSeed;
        _savedModifierSeed = _run.CurrentMapModifierSeed;
        _savedEncounterResolveCount = _run.EncounterResolveCount;
        _savedModifierResolveCount = _run.MapModifierResolveCount;
        _savedLootState = _run.Session.LootRandom.State;
        _savedCraftingState = _run.Session.CraftingRandom.State;
        _savedEventState = _run.Session.EventRandom.State;
    }

    private bool VerifyDeterministicState(string operation)
    {
        if (_run.CurrentEncounterId != _savedEncounterId
            || _run.CurrentEncounterSeed != _savedEncounterSeed
            || _run.CurrentMapModifierSeed != _savedModifierSeed
            || _run.EncounterResolveCount != _savedEncounterResolveCount
            || _run.MapModifierResolveCount != _savedModifierResolveCount
            || _run.Session.LootRandom.State != _savedLootState
            || _run.Session.CraftingRandom.State != _savedCraftingState
            || _run.Session.EventRandom.State != _savedEventState)
        {
            Fail($"{operation} rerolled encounter/modifier or changed run RNG");
            return false;
        }

        return VerifyPlanAndHud();
    }

    private void KillActiveEnemies()
    {
        var request = new DamageRequest(
            999999,
            DamageType.Physical,
            "encounter_plan_runtime_smoke",
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
        if (_complete)
        {
            return;
        }

        _complete = true;
        GD.PushError($"ENCOUNTER_PLAN_RUNTIME_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}
