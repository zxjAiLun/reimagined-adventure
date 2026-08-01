using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Covers pause safety and map-local director lifecycle across a real map
/// transition. The second map must create a fresh director and start its own
/// encounter rather than reusing the first map's counters.
/// </summary>
public partial class EncounterLifecycle3DRegressionSmoke : Node
{
    private enum TransitionStage
    {
        WaitingForSecondMap,
        WaitingForOldMapRelease,
        WaitingForSecondHealthHud,
        WaitingForFirstSpawnSafety,
        ClearingSecondEncounter,
        VerifyingSecondBossHud,
    }

    private double _elapsed;
    private double _transitionElapsed;
    private bool _complete;
    private bool _pauseTestStarted;
    private bool _pauseTestCompleted;
    private bool _transitionRequested;
    private int _spawnedBeforePause;
    private int _waveBeforePause;
    private EncounterDirector3D _firstDirector;
    private GameFlowController3D _firstFlow;
    private MapRewardNode3D _firstRewards;
    private TestArena3D _firstArena;
    private PlayerController3D _firstPlayer;
    private CombatHudController3D _firstHud;
    private RunSessionNode _run;
    private TestArena3D _secondArena;
    private PlayerController3D _secondPlayer;
    private EncounterDirector3D _secondDirector;
    private GameFlowController3D _secondFlow;
    private CombatHudController3D _secondHud;
    private BrimstoneColossusController3D _secondBoss;
    private TransitionStage _transitionStage = TransitionStage.WaitingForSecondMap;
    private int _secondHealthAfterDamage;

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_complete)
        {
            return;
        }

        if (_transitionRequested)
        {
            VerifySecondMap(delta);
            return;
        }

        BindFirstMap();
        if (_firstDirector == null || _firstFlow == null || _firstRewards == null || _run == null)
        {
            if (_elapsed > 8.0)
            {
                Fail("encounter lifecycle nodes did not become ready");
            }

            return;
        }

        if (!_pauseTestStarted && _firstDirector.State != EncounterDirectorState3D.Completed)
        {
            _pauseTestStarted = true;
            _spawnedBeforePause = _firstDirector.SpawnedEnemyCount;
            _waveBeforePause = _firstDirector.CurrentWaveIndex;
            _firstFlow.RestoreState(GameFlowState.GameOver);
            return;
        }

        if (_pauseTestStarted && !_pauseTestCompleted)
        {
            if (_elapsed < 1.0)
            {
                return;
            }

            if (_firstDirector.SpawnedEnemyCount != _spawnedBeforePause
                || _firstDirector.CurrentWaveIndex != _waveBeforePause)
            {
                Fail("paused encounter continued spawning or advancing waves");
                return;
            }

            _firstFlow.RestoreState(GameFlowState.Playing);
            _pauseTestCompleted = true;
        }

        if (_firstFlow.State == GameFlowState.Playing)
        {
            foreach (var enemy in _firstDirector.GetActiveEnemies())
            {
                ApplyLethalDamage(enemy);
            }
        }

        if (_firstFlow.State == GameFlowState.MapComplete)
        {
            if (!_firstRewards.HasChosen && !_firstRewards.TryChooseReward(0))
            {
                Fail("first-map reward choice failed");
                return;
            }

            if (!_run.LoadNextMap())
            {
                Fail("next-map transition was rejected");
                return;
            }

            _transitionRequested = true;
            return;
        }

        if (_elapsed > 24.0)
        {
            Fail($"lifecycle smoke timed out state={_firstFlow.State} wave={_firstDirector.CurrentWaveIndex}");
        }
    }

    private void BindFirstMap()
    {
        _run ??= GetTree().GetFirstNodeInGroup("run_sessions") as RunSessionNode;
        if (_firstDirector != null && GodotObject.IsInstanceValid(_firstDirector))
        {
            return;
        }

        var arena = GetTree().GetNodesInGroup("arena_3d")
            .OfType<TestArena3D>()
            .FirstOrDefault(map => map.UsesEncounterRuntime);
        _firstArena = arena;
        _firstPlayer = arena?.GetNodeOrNull<PlayerController3D>("Player3D");
        _firstHud = arena?.GetNodeOrNull<CombatHudController3D>("HUD");
        _firstDirector = arena?.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        _firstFlow = arena?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        _firstRewards = arena?.GetNodeOrNull<MapRewardNode3D>("MapRewards3D");
    }

    private void VerifySecondMap(double delta)
    {
        _transitionElapsed += delta;
        _secondArena ??= GetTree().GetNodesInGroup("arena_3d")
            .OfType<TestArena3D>()
            .LastOrDefault(map => map.UsesEncounterRuntime
                && !ReferenceEquals(map, _firstArena));
        _secondPlayer ??= _secondArena?.GetNodeOrNull<PlayerController3D>("Player3D");
        _secondDirector ??= _secondArena?.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        _secondFlow ??= _secondArena?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        _secondHud ??= _secondArena?.GetNodeOrNull<CombatHudController3D>("HUD");

        if (_secondArena == null || _secondPlayer == null || _secondDirector == null
            || _secondFlow == null || _secondHud == null || _run.CurrentMapLevel != 2)
        {
            if (_transitionElapsed > 12.0)
            {
                Fail($"second map nodes did not become ready level={_run.CurrentMapLevel} arena={_secondArena != null} player={_secondPlayer != null} director={_secondDirector != null} flow={_secondFlow != null} hud={_secondHud != null}");
            }

            return;
        }

        if (_transitionStage == TransitionStage.WaitingForSecondMap)
        {
            if (!ReferenceEquals(_secondDirector.Player, _secondPlayer)
                || !ReferenceEquals(_secondHud.BoundPlayer, _secondPlayer)
                || !ReferenceEquals(_secondHud.BoundEncounterDirector, _secondDirector)
                || !ReferenceEquals(_secondHud.BoundFlow, _secondFlow)
                || !_secondDirector.IsOperational
                || _secondDirector.State == EncounterDirectorState3D.Disabled)
            {
                Fail("second map runtime nodes were not bound to their local player, director, flow, and HUD");
                return;
            }

            var feralSpawn = _secondArena.GetNodeOrNull<EncounterSpawnPoint3D>("SpawnPoints/FeralNorth");
            if (feralSpawn == null)
            {
                Fail("second map is missing the FeralNorth spawn point");
                return;
            }

            _secondPlayer.GlobalPosition = feralSpawn.GlobalPosition;
            _transitionStage = TransitionStage.WaitingForOldMapRelease;
            return;
        }

        if (_transitionStage == TransitionStage.WaitingForOldMapRelease)
        {
            if (GodotObject.IsInstanceValid(_firstArena)
                || GodotObject.IsInstanceValid(_firstPlayer)
                || GodotObject.IsInstanceValid(_firstHud)
                || GodotObject.IsInstanceValid(_firstDirector))
            {
                if (_transitionElapsed > 4.0)
                {
                    Fail("old map nodes were not released after the next map became ready");
                }

                return;
            }

            var beforeHealth = _secondPlayer.CurrentHealth;
            var result = _secondPlayer.ApplyDamage(new DamageRequest(
                5,
                DamageType.Physical,
                "encounter_lifecycle_second_map_health",
                CombatFaction.Enemy));
            if (result.DamageApplied <= 0)
            {
                Fail("second map player did not accept the lifecycle health test damage");
                return;
            }

            _secondHealthAfterDamage = _secondPlayer.CurrentHealth;
            if (_secondHealthAfterDamage >= beforeHealth)
            {
                Fail("second map player health did not decrease for HUD binding test");
                return;
            }

            _transitionStage = TransitionStage.WaitingForSecondHealthHud;
            return;
        }

        if (_transitionStage == TransitionStage.WaitingForSecondHealthHud)
        {
            if (_secondHud.PlayerCurrentHealth != _secondHealthAfterDamage
                || _secondHud.PlayerCurrentHealth != _secondPlayer.CurrentHealth)
            {
                Fail($"second map HUD did not follow local player health hud={_secondHud.PlayerCurrentHealth} player={_secondPlayer.CurrentHealth}");
                return;
            }

            _transitionStage = TransitionStage.WaitingForFirstSpawnSafety;
            return;
        }

        if (_transitionStage == TransitionStage.WaitingForFirstSpawnSafety)
        {
            if (_secondDirector.SpawnedEnemyCount == 0)
            {
                if (_transitionElapsed > 12.0)
                {
                    Fail("second map did not spawn an enemy after the local spawn safety setup");
                }

                return;
            }

            var feralSpawn = _secondArena.GetNodeOrNull<EncounterSpawnPoint3D>("SpawnPoints/FeralNorth");
            var minimumDistance = feralSpawn?.MinimumPlayerDistance ?? 0.0f;
            foreach (var enemy in _secondDirector.GetActiveEnemies())
            {
                if (enemy is FeralController3D feral
                    && (!ReferenceEquals(feral.RunSession, _run)
                        || !ReferenceEquals(feral.TargetPlayer, _secondPlayer)))
                {
                    Fail("second-map Feral did not bind the owning RunSession and local Player");
                    return;
                }

                if (enemy is SpitterController3D spitter
                    && (!ReferenceEquals(spitter.RunSession, _run)
                        || !ReferenceEquals(spitter.TargetPlayer, _secondPlayer)))
                {
                    Fail("second-map Spitter did not bind the owning RunSession and local Player");
                    return;
                }

                var distance = enemy.GlobalPosition.DistanceTo(_secondPlayer.GlobalPosition);
                if (distance + 0.001f < minimumDistance)
                {
                    Fail($"second map enemy spawned inside player safety distance distance={distance:0.00} minimum={minimumDistance:0.00}");
                    return;
                }
            }

            _transitionStage = TransitionStage.ClearingSecondEncounter;
            return;
        }

        if (_transitionStage == TransitionStage.ClearingSecondEncounter)
        {
            if (_secondFlow.State != GameFlowState.Playing)
            {
                Fail($"second map left Playing before Boss HUD verification state={_secondFlow.State}");
                return;
            }

            foreach (var enemy in _secondDirector.GetActiveEnemies())
            {
                if (enemy is not BrimstoneColossusController3D)
                {
                    ApplyLethalDamage(enemy);
                }
            }

            _secondBoss = _secondDirector.ActiveBoss as BrimstoneColossusController3D;
            if (_secondBoss != null)
            {
                if (!ReferenceEquals(_secondBoss.RunSession, _run)
                    || !ReferenceEquals(_secondBoss.TargetPlayer, _secondPlayer))
                {
                    Fail("second-map Boss did not bind the owning RunSession and local Player");
                    return;
                }

                _transitionStage = TransitionStage.VerifyingSecondBossHud;
            }

            return;
        }

        if (_transitionStage == TransitionStage.VerifyingSecondBossHud)
        {
            if (!_secondHud.BossPanelVisible
                || _secondHud.BossCurrentHealth != _secondBoss.CurrentHealth
                || _secondHud.BossHealthBarValue != _secondBoss.CurrentHealth
                || !ReferenceEquals(_secondHud.BoundEncounterDirector, _secondDirector))
            {
                if (_transitionElapsed > 20.0)
                {
                    Fail($"second map Boss HUD did not bind local Boss hudVisible={_secondHud.BossPanelVisible} hud={_secondHud.BossCurrentHealth} boss={_secondBoss.CurrentHealth}");
                }

                return;
            }

            _complete = true;
            GD.Print("ENCOUNTER_LIFECYCLE_3D_SPIKE_PASS pause_frozen=true map_transition=true local_player=true local_director=true local_flow=true old_map_released=true second_health_hud=true spawn_safety=true boss_hud=true level=2");
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            GetTree().Quit();
        }
    }

    private static void ApplyLethalDamage(Node3D enemy)
    {
        var request = new DamageRequest(
            9999,
            DamageType.Physical,
            "encounter_lifecycle_smoke",
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

    private void Fail(string reason)
    {
        _complete = true;
        GD.PushError($"ENCOUNTER_LIFECYCLE_3D_SPIKE_FAIL {reason}");
        GetTree().Quit(1);
    }
}
