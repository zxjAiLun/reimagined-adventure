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
        _stage = Stage.Complete;
        if (!VerifyForcedCompositions())
        {
            return;
        }

        _complete = true;
        GD.Print("ENCOUNTER_PLAN_RUNTIME_3D_REGRESSION_PASS pre_ready=true map_transition=true hud=true save_stable=true pause_stable=true forced_composition=true");
        GetTree().Quit();
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

    private static bool VerifyForcedCompositions()
    {
        return VerifyComposition(
            "res://resources/encounters/QuietCoastSkirmish3D.tres",
            3,
            10,
            7,
            2,
            1,
            5)
            && VerifyComposition(
                "res://resources/encounters/CrossfireAdvance3D.tres",
                4,
                15,
                10,
                4,
                1,
                6)
            && VerifyComposition(
                "res://resources/encounters/SiegePressure3D.tres",
                4,
                18,
                10,
                7,
                1,
                6);
    }

    private static bool VerifyComposition(
        string resourcePath,
        int waveCount,
        int totalCount,
        int feralCount,
        int spitterCount,
        int bossCount,
        int maxAlive)
    {
        var definition = GD.Load<EncounterDefinitionResource3D>(resourcePath);
        var maximumAlive = 0;
        foreach (var wave in definition?.Waves ?? new Godot.Collections.Array<EncounterWaveResource3D>())
        {
            maximumAlive = Mathf.Max(maximumAlive, wave.MaxAlive);
        }

        var definitionError = string.Empty;
        var valid = definition != null && definition.IsValid(out definitionError);

        if (!valid
            || definition.Waves.Count != waveCount
            || maximumAlive > maxAlive
            || definition.Waves.Last().Entries.Any(entry => !entry.EnemyScene.ResourcePath.Contains("Brimstone")))
        {
            return false;
        }

        var feral = 0;
        var spitter = 0;
        var boss = 0;
        foreach (var wave in definition.Waves)
        {
            foreach (var entry in wave.Entries)
            {
                var path = entry.EnemyScene.ResourcePath;
                if (path.Contains("Feral"))
                {
                    feral += entry.Count;
                }
                else if (path.Contains("Spitter"))
                {
                    spitter += entry.Count;
                }
                else if (path.Contains("Brimstone"))
                {
                    boss += entry.Count;
                }
            }
        }

        var matches = definition.Waves.Sum(wave => wave.TotalSpawnCount) == totalCount
            && feral == feralCount
            && spitter == spitterCount
            && boss == bossCount;
        return matches;
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
