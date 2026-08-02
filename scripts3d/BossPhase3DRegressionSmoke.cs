using System;
using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Regression coverage for the Brimstone phase contract. It uses the normal
/// legacy arena fixtures so the smoke can isolate one boss without waiting
/// through a full encounter, while phase adds still use the production
/// EncounterDirector spawn path.
/// </summary>
public partial class BossPhase3DRegressionSmoke : Node
{
    private double _elapsed;
    private int _stage;
    private bool _complete;
    private TestArena3D _arena;
    private BrimstoneColossusController3D _boss;
    private PlayerController3D _player;
    private EncounterDirector3D _director;
    private GameFlowController3D _flow;
    private int _ringHealthBefore;
    private int _phaseTwoAddCount;
    private readonly List<Vector3> _barrageDirections = new();
    private BossLavaEruption3D _pausedLava;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_complete)
        {
            return;
        }

        BindRuntime();
        if (_boss == null || _player == null || _director == null || _flow == null)
        {
            if (_elapsed > 10.0)
            {
                Fail("Boss phase smoke runtime did not become ready");
            }

            return;
        }

        switch (_stage)
        {
            case 0:
                PrepareArena();
                break;
            case 1:
                EnterPhaseTwo();
                break;
            case 2:
                ObserveMoltenRing();
                break;
            case 3:
                EnterPhaseThree();
                break;
            case 4:
                ObserveEmberBarrage();
                break;
            case 5:
                ObserveLavaAndPause();
                break;
            case 6:
                ObserveDeathBoundary();
                break;
        }

        if (_elapsed > 28.0 && !_complete)
        {
            Fail($"Boss phase smoke timed out at stage {_stage}");
        }
    }

    private void BindRuntime()
    {
        _arena ??= GetTree().GetNodesInGroup("arena_3d")
            .OfType<TestArena3D>()
            .FirstOrDefault();
        _boss ??= _arena?.GetNodeOrNull<BrimstoneColossusController3D>("BrimstoneColossus3D");
        _player ??= _arena?.GetNodeOrNull<PlayerController3D>("Player3D");
        _director ??= _arena?.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        _flow ??= _arena?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        if (_director != null)
        {
            _director.Enabled = false;
            _director.ProcessMode = ProcessModeEnum.Disabled;
        }
    }

    private void PrepareArena()
    {
        _player.GetNode<HealthComponent>("HealthComponent").SetMaxHealth(100000);
        _player.GlobalPosition = Vector3.Zero;
        _boss.GlobalPosition = new Vector3(5.0f, 0.0f, 0.0f);
        _boss.SetPhysicsProcess(false);
        DisableOtherFixtures();
        if (_boss.PhaseCount != 3 || _boss.CurrentPhaseIndex != 0 || !_boss.IsAlive)
        {
            Fail($"initial phase contract was invalid count={_boss.PhaseCount} index={_boss.CurrentPhaseIndex} alive={_boss.IsAlive}");
            return;
        }

        _stage = 1;
        _elapsed = 0.0;
    }

    private void EnterPhaseTwo()
    {
        if (_boss.CurrentPhaseIndex == 0)
        {
            _boss.ApplyDamage(new DamageRequest(
                50,
                DamageType.Physical,
                "boss_phase_smoke_phase_two",
                CombatFaction.Player));
            return;
        }

        _phaseTwoAddCount = _boss.BossAddSpawnCount;
        if (_boss.CurrentPhaseIndex != 1
            || _boss.CurrentPhaseId != "phase-2"
            || _boss.PhaseTransitionCount != 1
            || _phaseTwoAddCount != 2
            || _director.ActiveEnemyCount < 2)
        {
            Fail($"phase two transition/add contract failed phase={_boss.CurrentPhaseId} transitions={_boss.PhaseTransitionCount} adds={_phaseTwoAddCount} active={_director.ActiveEnemyCount}");
            return;
        }

        DisableOtherFixtures();
        _player.GlobalPosition = new Vector3(3.0f, 0.0f, 0.0f);
        _boss.GlobalPosition = Vector3.Zero;
        _boss.SetPhysicsProcess(true);
        _stage = 2;
        _elapsed = 0.0;
    }

    private void ObserveMoltenRing()
    {
        if (_boss.ActiveRingTelegraph == null)
        {
            if (_elapsed > 3.0)
            {
                Fail("Molten Ring did not create a telegraph");
            }

            return;
        }

        var telegraph = _boss.ActiveRingTelegraph;
        var ringCenter = new Vector3(_boss.LockedRingCenter.X, 0.05f, _boss.LockedRingCenter.Z);
        var geometryPass = telegraph.StartPosition.DistanceTo(ringCenter) < 0.01f
            && Math.Abs(telegraph.InnerRadius - _boss.RingInnerRadius) < 0.001f
            && Math.Abs(telegraph.OuterRadius - _boss.RingOuterRadius) < 0.001f
            && _boss.CurrentAttackId == "molten_ring";
        _ringHealthBefore = _player.CurrentHealth;
        if (!geometryPass)
        {
            Fail("Molten Ring telegraph geometry or attack id was not locked");
            return;
        }

        _boss.GlobalPosition = new Vector3(0.1f, 0.0f, 0.0f);
        if (_boss.MoltenRingImpactCount == 0)
        {
            return;
        }

        var hitOnce = _boss.MoltenRingImpactCount == 1
            && _player.CurrentHealth < _ringHealthBefore
            && _boss.ActiveRingTelegraph == null;
        if (!hitOnce)
        {
            Fail($"Molten Ring impact contract failed impacts={_boss.MoltenRingImpactCount} hp={_player.CurrentHealth}/{_ringHealthBefore} telegraph={_boss.ActiveRingTelegraph != null}");
            return;
        }

        _boss.SetPhysicsProcess(false);
        _stage = 3;
        _elapsed = 0.0;
    }

    private void EnterPhaseThree()
    {
        if (_boss.CurrentPhaseIndex < 2)
        {
            _boss.ApplyDamage(new DamageRequest(
                60,
                DamageType.Physical,
                "boss_phase_smoke_phase_three",
                CombatFaction.Player));
            return;
        }

        if (_boss.CurrentPhaseId != "phase-3"
            || _boss.PhaseTransitionCount != 2
            || _boss.RecoverySeconds >= 0.65f)
        {
            Fail($"phase three transition contract failed phase={_boss.CurrentPhaseId} transitions={_boss.PhaseTransitionCount} recovery={_boss.RecoverySeconds}");
            return;
        }

        _player.GlobalPosition = new Vector3(3.0f, 0.0f, 0.0f);
        _boss.SetPhysicsProcess(true);
        _stage = 4;
        _elapsed = 0.0;
    }

    private void ObserveEmberBarrage()
    {
        if (_boss.ActiveBarrageTelegraphCount == 3 && _barrageDirections.Count == 0)
        {
            for (var index = 0; index < _boss.LockedBarrageDirectionCount; index++)
            {
                _barrageDirections.Add(_boss.GetLockedBarrageDirection(index));
            }
            _player.GlobalPosition = new Vector3(-4.0f, 0.0f, 2.0f);
        }

        if (_barrageDirections.Count == 0)
        {
            if (_elapsed > 4.0)
            {
                Fail("Ember Barrage did not create three telegraphs");
            }

            return;
        }

        if (_boss.EmberBarrageLaunchCount == 0)
        {
            return;
        }

        var locked = _boss.LastBarrageLaunchDirectionCount == _barrageDirections.Count
            && Enumerable.Range(0, _boss.LastBarrageLaunchDirectionCount)
                .Select(index => _boss.GetLastBarrageLaunchDirection(index)
                    .DistanceTo(_barrageDirections[index]) < 0.001f)
                .All(value => value);
        if (!locked || _boss.ActiveBarrageTelegraphCount != 0)
        {
            Fail("Ember Barrage retargeted or left telegraphs active after launch");
            return;
        }

        _stage = 5;
        _elapsed = 0.0;
    }

    private void ObserveLavaAndPause()
    {
        if (_boss.LavaEruptionCount == 0 || _boss.ActiveLavaEruption == null)
        {
            if (_elapsed > 4.0)
            {
                Fail("phase three did not create deterministic lava eruption");
            }

            return;
        }

        _pausedLava = _boss.ActiveLavaEruption;
        var seed = _boss.LastHazardSeed;
        if (seed == 0 || _pausedLava.ActiveTelegraph == null || _pausedLava.DeterministicSeed != seed)
        {
            Fail("lava eruption lost its deterministic seed or telegraph");
            return;
        }

        _flow.RestoreState(GameFlowState.GameOver);
        var cancelled = _pausedLava.IsCancelled || !GodotObject.IsInstanceValid(_pausedLava);
        _flow.RestoreState(GameFlowState.Playing);
        if (!cancelled)
        {
            Fail("GameOver did not cancel the active lava eruption");
            return;
        }

        _stage = 6;
        _elapsed = 0.0;
    }

    private void ObserveDeathBoundary()
    {
        _boss.ApplyDamage(new DamageRequest(
            999999,
            DamageType.Physical,
            "boss_phase_smoke_lethal",
            CombatFaction.Player));
        if (_boss.IsAlive)
        {
            if (_elapsed > 2.0)
            {
                Fail("Boss did not die from lethal phase smoke damage");
            }

            return;
        }

        var cleaned = _boss.State == BrimstoneColossusState3D.Dead
            && !_boss.IsPhysicsProcessing()
            && _boss.CollisionLayer == 0
            && _boss.CollisionMask == 0
            && _boss.ActiveRingTelegraph == null
            && _boss.ActiveBarrageTelegraphCount == 0
            && _flow.State == GameFlowState.MapComplete;
        if (!cleaned)
        {
            Fail($"Boss death boundary was incomplete state={_boss.State} physics={_boss.IsPhysicsProcessing()} flow={_flow.State}");
            return;
        }

        _complete = true;
        GD.Print("BOSS_PHASE_3D_REGRESSION_PASS phases=true adds=true ring=true ring_geometry=true barrage=true barrage_lock=true lava=true deterministic_seed=true pause_cancel=true death_cleanup=true map_complete=true");
        GetTree().Quit();
    }

    private void DisableOtherFixtures()
    {
        foreach (var node in GetTree().GetNodesInGroup("enemies_3d"))
        {
            if (node is not Node3D enemy || ReferenceEquals(enemy, _boss))
            {
                continue;
            }

            enemy.SetPhysicsProcess(false);
            if (enemy is CollisionObject3D collision)
            {
                collision.CollisionLayer = 0;
                collision.CollisionMask = 0;
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
        GD.PushError($"BOSS_PHASE_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}
