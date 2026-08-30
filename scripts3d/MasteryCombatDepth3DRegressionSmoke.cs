using System;
using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Mastery milestone end-to-end contract.
///
/// This is deliberately a real run rather than a collection of direct state
/// injections: Map 1 -> Map 2 -> Map 3 -> Map 4 uses the formal Atlas route,
/// the map-local encounter director, the build intermission and the normal
/// Brimstone phase runtime. The smaller regression scenes remain responsible
/// for the individual rule contracts; this scene proves that their ownership
/// boundaries survive one complete run.
/// </summary>
public partial class MasteryCombatDepth3DRegressionSmoke : Node
{
    private enum Stage
    {
        BindMap1,
        ClearMap1,
        BuildMap1,
        WaitMap2,
        ClearMap2,
        BuildMap2,
        WaitMap3,
        ClearMap3,
        BuildMap3,
        WaitMap4,
        ClearMap4,
        BossPhaseTwo,
        BossPhaseThree,
        Finalize,
    }

    private Stage _stage = Stage.BindMap1;
    private double _elapsed;
    private double _stageElapsed;
    private bool _complete;
    private RunSessionNode _run;
    private TestArena3D _arena;
    private TestArena3D _previousArena;
    private EncounterDirector3D _director;
    private GameFlowController3D _flow;
    private MapRewardNode3D _rewards;
    private BuildIntermissionController3D _build;
    private AtlasRouteChoiceController3D _route;
    private SaveBoundaryNode3D _save;
    private PlayerController3D _player;
    private PlayerSkillController3D _skills;
    private BrimstoneColossusController3D _boss;

    private readonly HashSet<string> _eliteIds = new(StringComparer.Ordinal);
    private int _map2Experience;
    private int _map3Experience;
    private int _map4Experience;
    private bool _initialVolleyVerified;
    private bool _map1BuildApplied;
    private bool _map2CombatPrepared;
    private bool _map2BuildApplied;
    private bool _map3BuildApplied;
    private bool _phaseTwoDamageApplied;
    private bool _phaseThreeDamageApplied;
    private bool _ringObserved;
    private bool _barrageObserved;
    private bool _lavaObserved;
    private bool _bossKillApplied;
    private int _bossAddCount;
    private int _ringImpactCount;
    private int _barrageLaunchCount;
    private int _lavaCount;
    private int _savedLevel;
    private int _savedExperience;
    private string[] _savedInventoryIds = Array.Empty<string>();
    private string[] _savedStashIds = Array.Empty<string>();
    private string _savedWeaponId = string.Empty;
    private int _savedForgeFragments;
    private ulong _savedEventRandomState;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        if (_complete)
        {
            return;
        }

        _elapsed += delta;
        _stageElapsed += delta;
        if (_elapsed > 360.0)
        {
            Fail($"mastery run timed out at stage {_stage} map={_run?.CurrentAtlasMapId}");
            return;
        }

        try
        {
            BindRun();
            switch (_stage)
            {
                case Stage.BindMap1:
                    TryBindMap(1, "quiet-coast");
                    break;
                case Stage.ClearMap1:
                    TickClearMap(1);
                    break;
                case Stage.BuildMap1:
                    TickBuildMap1();
                    break;
                case Stage.WaitMap2:
                    TryBindMap(2, "volatile-rift");
                    break;
                case Stage.ClearMap2:
                    TickClearMap2();
                    break;
                case Stage.BuildMap2:
                    TickBuildMap2();
                    break;
                case Stage.WaitMap3:
                    TryBindMap(3, "hardened-frontier");
                    break;
                case Stage.ClearMap3:
                    TickClearMap3();
                    break;
                case Stage.BuildMap3:
                    TickBuildMap3();
                    break;
                case Stage.WaitMap4:
                    TryBindMap(4, "brimstone-caldera");
                    break;
                case Stage.ClearMap4:
                    TickClearMap4();
                    break;
                case Stage.BossPhaseTwo:
                    TickBossPhaseTwo();
                    break;
                case Stage.BossPhaseThree:
                    TickBossPhaseThree();
                    break;
                case Stage.Finalize:
                    TickFinalize();
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }

        if (!_complete && _stageElapsed > 120.0)
        {
            Fail($"mastery stage {_stage} timed out map={_run?.CurrentAtlasMapId} wave={_director?.CurrentWaveIndex} "
                + $"spawned={_director?.CurrentWaveSpawnedCount}/{_director?.CurrentWaveTargetCount} "
                + $"active={_director?.ActiveEnemyCount} state={_director?.State} process={_director?.ProcessMode} "
                + $"completed={_director?.EncounterCompletedCount} flowBind={_flow?.IsEncounterBindingReady} "
                + $"elites={_eliteIds.Count} bossAlive={_boss?.IsAlive}");
        }
    }

    private void BindRun()
    {
        _run ??= GetNodeOrNull<RunSessionNode>("RunShell3D");
    }

    private void TryBindMap(int expectedLevel, string expectedAtlasId)
    {
        if (_run == null)
        {
            return;
        }

        var candidate = _run.CurrentMap3D;
        if (!IsValid(candidate) || ReferenceEquals(candidate, _previousArena))
        {
            candidate = GetTree().GetNodesInGroup("arena_3d")
                .OfType<TestArena3D>()
                .LastOrDefault(map => map.UsesEncounterRuntime
                    && !ReferenceEquals(map, _previousArena));
        }

        if (!IsValid(candidate)
            || _run.CurrentMapLevel != expectedLevel
            || _run.CurrentAtlasMapId != expectedAtlasId
            || candidate.AppliedRunPlan?.MapLevel != expectedLevel
            || candidate.AppliedRunPlan?.AtlasMapId != expectedAtlasId)
        {
            if (_stageElapsed > 12.0)
            {
                Fail($"map {expectedLevel} did not become ready id={_run.CurrentAtlasMapId} level={_run.CurrentMapLevel}");
            }

            return;
        }

        if (IsValid(_previousArena))
        {
            if (_stageElapsed > 6.0)
            {
                Fail($"old map remained alive during transition to {expectedAtlasId}");
            }

            return;
        }

        if (!ReferenceEquals(_arena, candidate))
        {
            BindMapNodes(candidate);
        }

        if (_director == null || !_director.IsOperational)
        {
            return;
        }

        if (_stage == Stage.BindMap1)
        {
            if (_run.CurrentMapModifierId != "quiet-coast"
                || _run.CurrentEncounterId != "quiet_coast_skirmish")
            {
                Fail("Map 1 did not resolve the Quiet Coast mastery baseline");
                return;
            }

            var initialSupport = _skills.Supports(SkillSlot.Primary).SingleOrDefault();
            _initialVolleyVerified = initialSupport?.Id == "volley"
                && _skills.SupportCount(SkillSlot.Primary) == 1
                && SkillSupportMath.ProjectileCount(
                    _skills.Definition(SkillSlot.Primary),
                    _player.EffectiveStats,
                    _skills.Supports(SkillSlot.Primary)) == 5;
            if (!_initialVolleyVerified)
            {
                Fail("Map 1 default supported skill loadout was not applied");
                return;
            }

            SetStage(Stage.ClearMap1);
        }
        else if (_stage == Stage.WaitMap2)
        {
            if (_run.CurrentMapModifierId != "volatile-hunt"
                || _run.CurrentEncounterId != "crossfire_advance"
                || _run.CurrentEncounterTier != 2)
            {
                Fail("Map 2 route did not resolve Volatile Rift / Crossfire Advance");
                return;
            }

            SetStage(Stage.ClearMap2);
        }
        else if (_stage == Stage.WaitMap3)
        {
            if (_run.CurrentMapModifierId != "hardened-front"
                || _run.CurrentEncounterId != "crossfire_advance"
                || _run.CurrentEncounterTier != 2)
            {
                Fail("Map 3 route did not resolve Hardened Frontier / Crossfire Advance");
                return;
            }

            SetStage(Stage.ClearMap3);
        }
        else if (_stage == Stage.WaitMap4)
        {
            if (_run.CurrentMapModifierId != "volatile-hunt"
                || _run.CurrentEncounterId != "siege_pressure"
                || _run.CurrentEncounterTier != 3)
            {
                Fail("Map 4 route did not resolve Brimstone Caldera / Siege Pressure");
                return;
            }

            SetStage(Stage.ClearMap4);
        }
    }

    private void BindMapNodes(TestArena3D map)
    {
        _arena = map;
        _director = map.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        _flow = map.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        _rewards = map.GetNodeOrNull<MapRewardNode3D>("MapRewards3D");
        _build = map.GetNodeOrNull<BuildIntermissionController3D>("BuildIntermission3D");
        _route = map.GetNodeOrNull<AtlasRouteChoiceController3D>("AtlasRouteChoice3D");
        _save = map.GetNodeOrNull<SaveBoundaryNode3D>("SaveBoundary3D");
        _player = map.GetNodeOrNull<PlayerController3D>("Player3D");
        _skills = _player?.Skills;
        _boss = null;
    }

    private void TickClearMap(int mapLevel)
    {
        if (_flow == null || _director == null)
        {
            return;
        }

        if (_flow.State == GameFlowState.MapComplete)
        {
            if (mapLevel == 1)
            {
                if (_run.TotalExperience != 72 || _run.CharacterLevel != 2)
                {
                    Fail($"Map 1 experience contract failed xp={_run.TotalExperience} level={_run.CharacterLevel}");
                    return;
                }

                SetStage(Stage.BuildMap1);
            }

            return;
        }

        if (_flow.State != GameFlowState.Playing)
        {
            Fail($"Map {mapLevel} ended in unexpected flow state {_flow.State}");
            return;
        }

        KillCurrentWaveEnemies(leaveBossAlive: false);
        if (mapLevel == 2 && _map2CombatPrepared)
        {
            _map2Experience = _run.TotalExperience;
        }
        if (mapLevel == 3)
        {
            _map3Experience = _run.TotalExperience;
        }
    }

    private void TickBuildMap1()
    {
        if (_flow.State != GameFlowState.MapComplete)
        {
            return;
        }

        if (!_rewards.HasChosen)
        {
            if (!_rewards.TryChooseReward(0))
            {
                return;
            }
        }

        if (!_build.IsBuildManagement)
        {
            return;
        }

        if (!_map1BuildApplied)
        {
            ApplyMap1Build();
            _map1BuildApplied = true;
        }

        if (!_build.TryCompleteBuildForTest() || !_route.ChoiceActive)
        {
            return;
        }

        ConfirmRoute("volatile-rift", Stage.WaitMap2);
    }

    private void ApplyMap1Build()
    {
        var playerBuild = _player.GetNode<PlayerBuildController3D>("PlayerBuildController3D");
        if (!playerBuild.IsOpen)
        {
            throw new InvalidOperationException("Map 1 reward did not open build management");
        }

        // Exercise the actual keyboard-first passive path once. Later maps use
        // the same run-owned API after the first UI contract is proven.
        SendKey(Key.V);
        SendKey(Key.G);
        if (!_run.PassiveTree.IsAllocated("sharpened-bolt")
            || _run.UnspentPassivePoints != 0
            || _player.PassiveStats.ProjectileDamageMultiplier <= 1.0)
        {
            throw new InvalidOperationException("Map 1 Sharpened Bolt was not allocated through the build UI");
        }

        CollectAvailableDrops();
        EnsureEquippedWeapon();
        EnsureStashItem();
        if (_player.EquippedWeapon == null
            || _player.EffectiveStats.DamageMultiplier <= 1.0
            || _build.StashItems.Count == 0
            || _build.Currency.ForgeFragments < 3)
        {
            throw new InvalidOperationException(
                $"Map 1 build continuity setup failed weapon={_player.EquippedWeaponName} damage={_player.EffectiveStats.DamageMultiplier:0.00} stash={_build.StashItems.Count} fragments={_build.Currency.ForgeFragments}");
        }

        if (!_skills.TryDetachSupport(SkillSlot.Primary)
            || !_skills.TryAttachSupport(SkillSlot.Primary, "frostbite")
            || !_skills.TryDetachSupport(SkillSlot.Secondary)
            || !_skills.TryAttachSupport(SkillSlot.Secondary, "combustion")
            || !_skills.TryAttachSupport(SkillSlot.Utility, "overload"))
        {
            throw new InvalidOperationException("Map 1 mastery support loadout could not be attached");
        }
    }

    private void TickClearMap2()
    {
        TickClearMap(2);
        if (_flow.State == GameFlowState.MapComplete)
        {
            if (!_map2CombatPrepared)
            {
                PrepareMap2CombatAndRecovery();
                if (!_map2CombatPrepared)
                {
                    return;
                }
            }

            if (_run.TotalExperience < 170)
            {
                Fail($"Map 2 progression XP contract failed elites={_eliteIds.Count} xp={_run.TotalExperience}");
                return;
            }

            SetStage(Stage.BuildMap2);
        }
    }

    private void TickBuildMap2()
    {
        if (_flow.State != GameFlowState.MapComplete)
        {
            return;
        }

        if (!_rewards.HasChosen && !_rewards.TryChooseReward(0))
        {
            return;
        }

        if (!_build.IsBuildManagement)
        {
            return;
        }

        if (!_map2BuildApplied)
        {
            if (!_run.TryAllocatePassive("wide-impact"))
            {
                throw new InvalidOperationException(
                    $"Map 2 Wide Impact allocation failed level={_run.CharacterLevel} points={_run.UnspentPassivePoints}");
            }

            _map2BuildApplied = true;
        }

        if (!_build.TryCompleteBuildForTest() || !_route.ChoiceActive)
        {
            return;
        }

        ConfirmRoute("hardened-frontier", Stage.WaitMap3);
    }

    private void TickClearMap3()
    {
        TickClearMap(3);
        if (_flow.State == GameFlowState.MapComplete)
        {
            SetStage(Stage.BuildMap3);
        }
    }

    private void TickBuildMap3()
    {
        if (_flow.State != GameFlowState.MapComplete)
        {
            return;
        }

        if (!_rewards.HasChosen && !_rewards.TryChooseReward(0))
        {
            return;
        }

        if (!_build.IsBuildManagement)
        {
            return;
        }

        if (!_map3BuildApplied)
        {
            if (!_run.TryAllocatePassive("kindling"))
            {
                throw new InvalidOperationException(
                    $"Map 3 Kindling allocation failed level={_run.CharacterLevel} points={_run.UnspentPassivePoints}");
            }

            _map3BuildApplied = true;
        }

        if (!_build.TryCompleteBuildForTest() || !_route.ChoiceActive)
        {
            return;
        }

        ConfirmRoute("brimstone-caldera", Stage.WaitMap4);
    }

    private void TickClearMap4()
    {
        if (_flow.State == GameFlowState.MapComplete)
        {
            Fail("Map 4 completed before the Brimstone phase contract ran");
            return;
        }

        if (_flow.State != GameFlowState.Playing)
        {
            Fail($"Map 4 ended in unexpected flow state {_flow.State}");
            return;
        }

        if (_director.CurrentWaveIndex < 0
            || _director.CurrentWaveSpawnedCount < _director.CurrentWaveTargetCount)
        {
            return;
        }

        _boss = _director.GetActiveEnemies()
            .OfType<BrimstoneColossusController3D>()
            .FirstOrDefault();
        if (_boss == null)
        {
            KillCurrentWaveEnemies(leaveBossAlive: false);
            return;
        }

        _player.GlobalPosition = _boss.GlobalPosition + new Vector3(10.0f, 0.0f, 4.0f);
        SetStage(Stage.BossPhaseTwo);
    }

    private void TickBossPhaseTwo()
    {
        if (!IsValid(_boss) || !_boss.IsAlive)
        {
            return;
        }

        if (BossMetaString("boss_phase_id") == "phase-1" && !_phaseTwoDamageApplied)
        {
            var damage = Mathf.Max(1, Mathf.CeilToInt(_boss.MaxHealth * 0.31f));
            _boss.ApplyDamage(new DamageRequest(
                damage,
                DamageType.Physical,
                "mastery_boss_phase_two",
                CombatFaction.Player));
            _phaseTwoDamageApplied = true;
            return;
        }

        if (BossMetaString("boss_phase_id") != "phase-2")
        {
            return;
        }

        _bossAddCount = BossMetaInt("boss_add_count");
        KillNonBossEnemies();
        var ring = FindActiveRing(_arena);
        if (ring != null)
        {
            _ringObserved = true;
        }

        if (_bossAddCount < 2
            || !_ringObserved
            || BossMetaInt("boss_molten_ring_impact_count") < 1)
        {
            return;
        }

        _ringImpactCount = BossMetaInt("boss_molten_ring_impact_count");
        if (!_phaseThreeDamageApplied)
        {
            var damage = Mathf.Max(1, Mathf.CeilToInt(_boss.MaxHealth * 0.42f));
            _boss.ApplyDamage(new DamageRequest(
                damage,
                DamageType.Physical,
                "mastery_boss_phase_three",
                CombatFaction.Player));
            _phaseThreeDamageApplied = true;
            SetStage(Stage.BossPhaseThree);
        }
    }

    private void TickBossPhaseThree()
    {
        if (!IsValid(_boss) || !_boss.IsAlive)
        {
            return;
        }

        if (BossMetaString("boss_phase_id") != "phase-3")
        {
            return;
        }

        if (!_barrageObserved && BossMetaInt("boss_active_barrage_telegraph_count") == 3)
        {
            _barrageObserved = true;
            _player.GlobalPosition = _boss.GlobalPosition + new Vector3(-10.0f, 0.0f, -4.0f);
        }

        if (_barrageObserved && BossMetaInt("boss_ember_barrage_launch_count") >= 1)
        {
            _barrageLaunchCount = BossMetaInt("boss_ember_barrage_launch_count");
        }

        if (!_lavaObserved && BossMetaInt("boss_lava_eruption_count") >= 1)
        {
            _lavaObserved = true;
            _lavaCount = BossMetaInt("boss_lava_eruption_count");
            _player.GlobalPosition = _boss.GlobalPosition + new Vector3(-10.0f, 0.0f, 6.0f);
        }

        if (!_barrageObserved
            || _barrageLaunchCount < 1
            || !_lavaObserved
            || !GodotObject.IsInstanceValid(_boss))
        {
            return;
        }

        if (!_bossKillApplied)
        {
            _boss.ApplyDamage(new DamageRequest(
                999999,
                DamageType.Physical,
                "mastery_boss_lethal",
                CombatFaction.Player));
            _bossKillApplied = true;
        }

        if (!_boss.IsAlive)
        {
            SetStage(Stage.Finalize);
        }
    }

    private void TickFinalize()
    {
        if (_flow.State != GameFlowState.MapComplete || _boss == null || _boss.IsAlive)
        {
            return;
        }

        if (_run.AtlasCompletionCount != 4
            || !_run.Atlas.State.IsCompleted("brimstone-caldera")
            || _run.CurrentAtlasMapId != "brimstone-caldera"
            || _bossAddCount < 2
            || _ringImpactCount != 1
            || _barrageLaunchCount < 1
            || _lavaCount < 1
            || _run.CharacterLevel < 4
            || !_run.PassiveTree.IsAllocated("sharpened-bolt")
            || !_run.PassiveTree.IsAllocated("wide-impact")
            || !_run.PassiveTree.IsAllocated("kindling")
            || _eliteIds.Count == 0)
        {
            Fail(
                $"mastery completion contract failed atlas={_run.AtlasCompletionCount}/{_run.CurrentAtlasMapId} "
                + $"adds={_bossAddCount} ring={_ringImpactCount} barrage={_barrageLaunchCount} lava={_lavaCount} "
                + $"level={_run.CharacterLevel} passives={string.Join(',', _run.PassiveTree.AllocatedNodeIds)} elites={_eliteIds.Count}");
            return;
        }

        if (!_rewards.HasChosen && !_rewards.TryChooseReward(0))
        {
            return;
        }

        if (!_build.IsBuildManagement)
        {
            return;
        }

        if (_savedLevel == 0)
        {
            CaptureFinalSaveState();
            if (!_save.TrySaveCurrentRun(out var saveError))
            {
                Fail($"final mastery save failed: {saveError}");
                return;
            }

            if (!_save.TryLoadAndApplyLastRun(out var restored, out var loadError))
            {
                Fail($"final mastery save restore failed: {loadError}");
                return;
            }

            var restoredIds = restored.InventoryItemIds.OrderBy(id => id).ToArray();
            var currentIds = _player.Items.Select(item => item.Id).OrderBy(id => id).ToArray();
            var currentStashIds = _build.StashItems.Select(item => item.Id).OrderBy(id => id).ToArray();
            var restoredPassives = restored.AllocatedPassiveNodeIds.OrderBy(id => id).ToArray();
            if (restored.TotalExperience != _savedExperience
                || restored.AllocatedPassiveNodeIds.Count != 3
                || !restoredPassives.SequenceEqual(
                    new[] { "kindling", "sharpened-bolt", "wide-impact" })
                || !restoredIds.SequenceEqual(_savedInventoryIds.OrderBy(id => id))
                || !currentIds.SequenceEqual(_savedInventoryIds.OrderBy(id => id))
                || !currentStashIds.SequenceEqual(_savedStashIds.OrderBy(id => id))
                || _player.EquippedWeapon?.Id != _savedWeaponId
                || _build.Currency.ForgeFragments != _savedForgeFragments
                || _run.Session.EventRandom.State != _savedEventRandomState
                || _skills.Supports(SkillSlot.Primary).SingleOrDefault()?.Id != "frostbite"
                || _skills.Supports(SkillSlot.Secondary).SingleOrDefault()?.Id != "combustion"
                || _skills.Supports(SkillSlot.Utility).SingleOrDefault()?.Id != "overload"
                || _player.Ailments.Collection.Active.Count != 0)
            {
                Fail(
                    $"final mastery persistence mismatch xp={restored.TotalExperience}/{_savedExperience} "
                    + $"inventory={string.Join(',', currentIds)} stash={string.Join(',', currentStashIds)} "
                    + $"weapon={_player.EquippedWeapon?.Id}/{_savedWeaponId} fragments={_build.Currency.ForgeFragments}/{_savedForgeFragments}");
                return;
            }

            _savedLevel = _run.CharacterLevel;
        }

        _complete = true;
        GD.Print(
            $"MASTERY_COMBAT_DEPTH_3D_REGRESSION_PASS maps=1-4 atlas=true "
            + $"xp={_run.TotalExperience} level={_run.CharacterLevel} passives=3 elites={_eliteIds.Count} "
            + $"ailments=true supports=true equipment=true stash=true forge={_build.Currency.ForgeFragments} "
            + $"boss_phase2_adds={_bossAddCount} ring={_ringImpactCount} barrage={_barrageLaunchCount} lava={_lavaCount} "
            + "save_restore=true transient_clean=true");
        FlushGodotWrappersAndQuit();
    }

    private void PrepareMap2CombatAndRecovery()
    {
        var burning = _player.ApplyDamage(CreateAilmentRequest(
            AilmentKind.Burning,
            DamageType.Fire,
            "mastery_map2_burning"));
        var chilled = _player.ApplyDamage(CreateAilmentRequest(
            AilmentKind.Chilled,
            DamageType.Cold,
            "mastery_map2_chilled"));
        var shocked = _player.ApplyDamage(CreateAilmentRequest(
            AilmentKind.Shocked,
            DamageType.Lightning,
            "mastery_map2_shocked"));
        if (burning.DamageApplied <= 0
            || chilled.DamageApplied <= 0
            || shocked.DamageApplied <= 0
            || _player.Ailments.Collection.Active.Count != 3)
        {
            throw new InvalidOperationException(
                $"Map 2 ailment runtime did not apply all three states: {_player.Ailments.Summary}");
        }

        if (!_save.TrySaveCurrentRun(out var saveError))
        {
            throw new InvalidOperationException($"Map 2 combat save failed: {saveError}");
        }

        _savedEventRandomState = _run.Session.EventRandom.State;
        if (!_save.TryLoadAndApplyLastRun(out _, out var loadError))
        {
            throw new InvalidOperationException($"Map 2 combat save restore failed: {loadError}");
        }

        if (_player.Ailments.Collection.Active.Count != 0
            || _run.Session.EventRandom.State != _savedEventRandomState)
        {
            throw new InvalidOperationException("Map 2 save restore did not clear transient ailments atomically");
        }

        _player.ApplyRestoredHealth(_player.MaxHealth);
        _map2CombatPrepared = true;
    }

    private DamageRequest CreateAilmentRequest(
        AilmentKind kind,
        DamageType type,
        string sourceId)
    {
        return new DamageRequest(
            1,
            type,
            sourceId,
            CombatFaction.Enemy,
            false,
            new AilmentApplicationDefinition(kind, 100));
    }

    private void KillCurrentWaveEnemies(bool leaveBossAlive)
    {
        if (_director.CurrentWaveIndex < 0
            || _director.CurrentWaveSpawnedCount < _director.CurrentWaveTargetCount)
        {
            return;
        }

        foreach (var enemy in _director.GetActiveEnemies().ToArray())
        {
            if (leaveBossAlive && enemy is BrimstoneColossusController3D)
            {
                continue;
            }

            RecordElite(enemy);
            if (enemy is ICombatTarget target)
            {
                target.ApplyDamage(new DamageRequest(
                    999999,
                    DamageType.Physical,
                    $"mastery_wave_{_director.CurrentWaveIndex}_{enemy.GetInstanceId()}",
                    CombatFaction.Player));
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
    }

    private void KillNonBossEnemies()
    {
        foreach (var enemy in _director.GetActiveEnemies().ToArray())
        {
            if (enemy is BrimstoneColossusController3D)
            {
                continue;
            }

            RecordElite(enemy);
            if (enemy is ICombatTarget target)
            {
                target.ApplyDamage(new DamageRequest(
                    999999,
                    DamageType.Physical,
                    $"mastery_boss_add_{enemy.GetInstanceId()}",
                    CombatFaction.Player));
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
    }

    private void RecordElite(Node3D enemy)
    {
        if (enemy is IEliteRuntime3D elite && !string.IsNullOrWhiteSpace(elite.EliteModifierId))
        {
            _eliteIds.Add(elite.EliteModifierId);
        }
    }

    private void EnsureEquippedWeapon()
    {
        var current = _player.EquippedWeapon;
        var best = _player.Items
            .Where(item => item.Slot == EquipmentSlot.Weapon)
            .OrderByDescending(item => item.Stats.DamageMultiplier)
            .FirstOrDefault();

        for (var attempt = 0; (best == null || best.Stats.DamageMultiplier <= 1.0) && attempt < 12; attempt++)
        {
            var candidate = _run.GenerateWeaponDrop(_run.CurrentMapPlan.DropItemLevel, boss: true);
            if (candidate.Stats.DamageMultiplier > 1.0 && _player.TryAddItem(candidate))
            {
                best = candidate;
                break;
            }
        }

        if (best != null
            && (current == null || best.Stats.DamageMultiplier > current.Stats.DamageMultiplier))
        {
            _player.TryEquipItem(best.Id);
        }
    }

    private void EnsureStashItem()
    {
        var candidate = _player.Items.FirstOrDefault(item =>
            _player.EquippedWeapon == null || item.Id != _player.EquippedWeapon.Id);
        if (candidate == null)
        {
            candidate = _run.GenerateItemDrop(new ItemRollContext(
                _run.CurrentMapPlan.DropItemLevel,
                LootSourceKind.Reward,
                EquipmentSlot.Armor,
                ForcedRarity: Rarity.Magic));
            if (!_player.TryAddItem(candidate))
            {
                return;
            }
        }

        _build.TryMoveInventoryToStash(candidate.Id);
    }

    private void CollectAvailableDrops()
    {
        for (var attempt = 0; attempt < 24; attempt++)
        {
            if (!_player.TryPickupNearest(100.0f))
            {
                break;
            }
        }
    }

    private void ConfirmRoute(string mapId, Stage nextStage)
    {
        var optionIndex = _route.OptionMapIds.ToList().IndexOf(mapId);
        if (optionIndex < 0 || !_route.TrySelect(optionIndex) || !_route.TryConfirm())
        {
            return;
        }

        _previousArena = _arena;
        _arena = null;
        _director = null;
        _flow = null;
        _rewards = null;
        _build = null;
        _route = null;
        _save = null;
        _player = null;
        _skills = null;
        SetStage(nextStage);
    }

    private void CaptureFinalSaveState()
    {
        _savedLevel = _run.CharacterLevel;
        _savedExperience = _run.TotalExperience;
        _savedInventoryIds = _player.Items.Select(item => item.Id).OrderBy(id => id).ToArray();
        _savedStashIds = _build.StashItems.Select(item => item.Id).OrderBy(id => id).ToArray();
        _savedWeaponId = _player.EquippedWeapon?.Id ?? string.Empty;
        _savedForgeFragments = _build.Currency.ForgeFragments;
        _savedEventRandomState = _run.Session.EventRandom.State;
    }

    private RingTelegraph3D FindActiveRing(Node node)
    {
        if (node is RingTelegraph3D ring && ring.IsActive)
        {
            return ring;
        }

        for (var index = 0; index < node.GetChildCount(); index++)
        {
            var child = node.GetChild(index);
            if (child is not null)
            {
                var result = FindActiveRing(child);
                if (result != null)
                {
                    return result;
                }
            }
        }

        return null;
    }

    private string BossMetaString(string key) =>
        _boss != null && _boss.HasMeta(key) ? _boss.GetMeta(key).AsString() : string.Empty;

    private int BossMetaInt(string key) =>
        _boss != null && _boss.HasMeta(key) ? _boss.GetMeta(key).AsInt32() : 0;

    private void SendKey(Key key)
    {
        var input = new InputEventKey
        {
            Keycode = key,
            PhysicalKeycode = key,
            Pressed = true,
        };
        GetViewport().PushInput(input);
    }

    private void SetStage(Stage stage)
    {
        _stage = stage;
        _stageElapsed = 0.0;
    }

    private static bool IsValid(GodotObject instance) =>
        instance != null && GodotObject.IsInstanceValid(instance);

    private void FlushGodotWrappersAndQuit()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GetTree().Quit(0);
    }

    private void Fail(string reason)
    {
        if (_complete)
        {
            return;
        }

        _complete = true;
        GD.PushError($"MASTERY_COMBAT_DEPTH_3D_REGRESSION_FAIL {reason}");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GetTree().Quit(1);
    }
}
