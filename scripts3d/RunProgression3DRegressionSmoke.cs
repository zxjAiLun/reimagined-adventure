using System;
using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Verifies the real two-map run contract: encounter spawn context, cumulative
/// rewards, continuous loot identity/random state, pause safety, and save
/// recovery. All combat clearing uses the normal enemy ApplyDamage path.
/// </summary>
public partial class RunProgression3DRegressionSmoke : Node
{
    private enum Stage
    {
        MapOne,
        WaitingForMapTwo,
        MapTwo,
        PausedMapTwo,
        Complete,
    }

    private Stage _stage = Stage.MapOne;
    private RunSessionNode _run;
    private TestArena3D _arena;
    private TestArena3D _firstArena;
    private PlayerController3D _player;
    private EncounterDirector3D _director;
    private GameFlowController3D _flow;
    private MapRewardNode3D _rewards;
    private SaveBoundaryNode3D _save;
    private bool _directorBound;
    private bool _mapOneContextSeen;
    private bool _mapTwoContextSeen;
    private bool _rewardAChosen;
    private bool _rewardBChosen;
    private bool _pauseChecked;
    private float _elapsed;
    private float _stageElapsed;
    private float _pauseElapsed;
    private int _itemSequenceBeforeMapTwo;
    private int _itemSequenceBeforePause;
    private int _waveBeforePause;
    private int _spawnedBeforePause;
    private int _expectedHealthAfterSave;
    private Stats _expectedReward;
    private ulong _expectedLootState;
    private ulong _expectedCraftingState;
    private ulong _expectedEventState;
    private readonly HashSet<string> _observedItemIds = new(StringComparer.Ordinal);
    private readonly HashSet<ItemDrop3D> _observedDrops = new();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        if (_stage == Stage.Complete)
        {
            return;
        }

        _elapsed += (float)delta;
        _stageElapsed += (float)delta;
        ScanDropIds();
        BindRuntime();
        if (_run == null || _arena == null || _player == null || _director == null || _flow == null || _rewards == null)
        {
            if (_elapsed > 12.0f)
            {
                Fail("run progression runtime did not become ready");
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
            case Stage.PausedMapTwo:
                TickPausedMapTwo();
                break;
        }

        if (_stageElapsed > 32.0f && _stage != Stage.Complete)
        {
            Fail($"stage {_stage} timed out map={_run.CurrentMapLevel} flow={_flow.State} wave={_director.CurrentWaveIndex}");
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
        _rewards = _arena.GetNodeOrNull<MapRewardNode3D>("MapRewards3D");
        _save = _arena.GetNodeOrNull<SaveBoundaryNode3D>("SaveBoundary3D");

        if (_director != null && (!_directorBound || !GodotObject.IsInstanceValid(_director)))
        {
            _director.EnemySpawned += OnEnemySpawned;
            _directorBound = true;
        }
    }

    private void TickMapOne()
    {
        if (_run.CurrentMapLevel != 1 || _flow.State != GameFlowState.Playing)
        {
            if (_flow.State == GameFlowState.MapComplete)
            {
                FinishMapOne();
            }

            return;
        }

        KillActiveEnemies();
        if (_flow.State == GameFlowState.MapComplete)
        {
            FinishMapOne();
        }
    }

    private void FinishMapOne()
    {
        if (!_mapOneContextSeen)
        {
            Fail("Map 1 did not produce a valid pre-ready spawn context");
            return;
        }

        if (!_rewardAChosen && !_rewards.TryChooseReward(0))
        {
            Fail("Map 1 Reward A could not be chosen");
            return;
        }

        _rewardAChosen = true;
        if (_player.RewardStats.DamageMultiplier != 1.20)
        {
            Fail($"Map 1 Reward A was not applied exactly once damage={_player.RewardStats.DamageMultiplier}");
            return;
        }

        _itemSequenceBeforeMapTwo = _run.ItemSequence;
        _firstArena = _arena;
        if (!_run.LoadNextMap())
        {
            Fail("Map 1 could not advance to Map 2");
            return;
        }

        _stage = Stage.WaitingForMapTwo;
        _stageElapsed = 0.0f;
        _arena = null;
        _director = null;
        _flow = null;
        _rewards = null;
        _save = null;
        _directorBound = false;
    }

    private void TickWaitingForMapTwo()
    {
        if (_run.CurrentMapLevel != 2 || _arena == null || ReferenceEquals(_arena, _firstArena))
        {
            return;
        }

        if (_director.Player != _player || !_director.IsOperational)
        {
            Fail("Map 2 encounter was not bound to the local Player");
            return;
        }

        _stage = Stage.MapTwo;
        _stageElapsed = 0.0f;
    }

    private void TickMapTwo()
    {
        if (_flow.State != GameFlowState.Playing)
        {
            if (_flow.State == GameFlowState.MapComplete)
            {
                FinishMapTwo();
            }

            return;
        }

        if (!_mapTwoContextSeen)
        {
            return;
        }

        if (!_pauseChecked)
        {
            _pauseChecked = true;
            _itemSequenceBeforePause = _run.ItemSequence;
            _waveBeforePause = _director.CurrentWaveIndex;
            _spawnedBeforePause = _director.SpawnedEnemyCount;
            _pauseElapsed = 0.0f;
            _flow.RestoreState(GameFlowState.GameOver);
            _stage = Stage.PausedMapTwo;
            return;
        }

        KillActiveEnemies();
        if (_flow.State == GameFlowState.MapComplete)
        {
            FinishMapTwo();
        }
    }

    private void TickPausedMapTwo()
    {
        _pauseElapsed += 0.0f;
        if (_pauseElapsed < 1.0f)
        {
            _pauseElapsed += 1.0f / 60.0f;
            return;
        }

        if (_run.ItemSequence != _itemSequenceBeforePause
            || _director.CurrentWaveIndex != _waveBeforePause
            || _director.SpawnedEnemyCount != _spawnedBeforePause)
        {
            Fail("GameOver pause changed run progression");
            return;
        }

        _flow.RestoreState(GameFlowState.Playing);
        _stage = Stage.MapTwo;
        _stageElapsed = 0.0f;
    }

    private void FinishMapTwo()
    {
        if (!_mapTwoContextSeen)
        {
            Fail("Map 2 did not produce a valid pre-ready spawn context");
            return;
        }

        if (!_rewardBChosen && !_rewards.TryChooseReward(1))
        {
            Fail("Map 2 Reward B could not be chosen");
            return;
        }

        _rewardBChosen = true;
        _expectedReward = _player.RewardStats;
        _expectedHealthAfterSave = _player.CurrentHealth;
        _expectedLootState = _run.Session.LootRandom.State;
        _expectedCraftingState = _run.Session.CraftingRandom.State;
        _expectedEventState = _run.Session.EventRandom.State;

        if (_expectedReward.DamageMultiplier != 1.20
            || _expectedReward.MaxHp != 1
            || _run.ItemSequence <= _itemSequenceBeforeMapTwo
            || _observedItemIds.Count != _run.ItemSequence)
        {
            Fail($"Map 2 cumulative progression was invalid damage={_expectedReward.DamageMultiplier} maxhp={_expectedReward.MaxHp} items={_run.ItemSequence} observed={_observedItemIds.Count}");
            return;
        }

        var saveError = string.Empty;
        if (_save == null || !_save.TrySaveCurrentRun(out saveError))
        {
            Fail($"Map 2 progression save failed: {saveError}");
            return;
        }

        _player.SetRewardStats(Stats.Neutral);
        _player.ApplyDamage(new DamageRequest(
            7,
            DamageType.Physical,
            "run_progression_save_perturbation",
            CombatFaction.Enemy));

        if (!_save.TryLoadAndApplyLastRun(out var state, out var restoreError))
        {
            Fail($"Map 2 progression restore failed: {restoreError}");
            return;
        }

        if (_run.CurrentMapLevel != 2
            || state.MapLevel != 2
            || !_player.RewardStats.EquivalentTo(_expectedReward)
            || _player.CurrentHealth != _expectedHealthAfterSave
            || _run.ItemSequence != state.ItemSequence
            || _run.Session.LootRandom.State != _expectedLootState
            || _run.Session.CraftingRandom.State != _expectedCraftingState
            || _run.Session.EventRandom.State != _expectedEventState)
        {
            Fail("Map 2 save/restore did not preserve progression state");
            return;
        }

        _stage = Stage.Complete;
        GD.Print("RUN_PROGRESSION_3D_REGRESSION_PASS levels=true scaling_context=true cumulative_rewards=true save_restore=true rng_continuity=true item_ids_unique=true pause_frozen=true");
        // Flush Godot-managed arrays before the process exits; the managed wrapper
        // finalizer must run while the native Godot runtime is still alive.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GetTree().Quit();
    }

    private void OnEnemySpawned(Node3D enemy)
    {
        var expectedLevel = _run.CurrentMapLevel;
        if (expectedLevel != 1 && expectedLevel != 2)
        {
            Fail($"unexpected encounter map level {expectedLevel}");
            return;
        }

        var valid = enemy switch
        {
            FeralController3D feral => VerifyEnemy(
                feral.AppliedMapLevel,
                feral.SpawnContextAppliedCount,
                feral.AppliedDropItemLevel,
                feral.RunSession,
                feral.TargetPlayer),
            SpitterController3D spitter => VerifyEnemy(
                spitter.AppliedMapLevel,
                spitter.SpawnContextAppliedCount,
                spitter.AppliedDropItemLevel,
                spitter.RunSession,
                spitter.TargetPlayer),
            BrimstoneColossusController3D boss => VerifyEnemy(
                boss.AppliedMapLevel,
                boss.SpawnContextAppliedCount,
                boss.AppliedDropItemLevel,
                boss.RunSession,
                boss.TargetPlayer),
            _ => false,
        };

        if (!valid)
        {
            Fail($"enemy spawn context invalid at map {expectedLevel}");
            return;
        }

        if (expectedLevel == 1)
        {
            _mapOneContextSeen = true;
        }
        else
        {
            _mapTwoContextSeen = true;
        }
    }

    private bool VerifyEnemy(
        int appliedMapLevel,
        int appliedCount,
        int dropLevel,
        RunSessionNode owningSession,
        PlayerController3D targetPlayer)
    {
        return appliedMapLevel == _run.CurrentMapLevel
            && appliedCount == 1
            && dropLevel == MapScaling.ItemLevel(
                _run.CurrentMapLevel,
                _run.CurrentMapModifier?.Effects ?? new MapModifierStats())
            && ReferenceEquals(owningSession, _run)
            && ReferenceEquals(targetPlayer, _player);
    }

    private void KillActiveEnemies()
    {
        foreach (var enemy in _director.GetActiveEnemies().ToArray())
        {
            var request = new DamageRequest(
                9999,
                DamageType.Physical,
                "run_progression_smoke",
                CombatFaction.Player);
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

    private void ScanDropIds()
    {
        foreach (var node in GetTree().GetNodesInGroup("item_drops_3d"))
        {
            if (node is not ItemDrop3D drop || drop.Item == null)
            {
                continue;
            }

            if (!_observedDrops.Add(drop))
            {
                continue;
            }

            if (!_observedItemIds.Add(drop.Item.Id))
            {
                Fail($"duplicate runtime Item.Id observed: {drop.Item.Id}");
                return;
            }
        }
    }

    private void Fail(string reason)
    {
        if (_stage == Stage.Complete)
        {
            return;
        }

        _stage = Stage.Complete;
        GD.PushError($"RUN_PROGRESSION_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}
