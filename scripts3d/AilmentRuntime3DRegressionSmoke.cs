using System;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Regression smoke for the runtime status contract: proc boundaries, the
/// incoming Shock multiplier, Burning ticks, pause safety, and death cleanup.
/// </summary>
public partial class AilmentRuntime3DRegressionSmoke : Node
{
    private TestArena3D _arena;
    private PlayerController3D _player;
    private FeralController3D _feral;
    private FeralController3D _supportFeral;
    private GameFlowController3D _flow;
    private RunSessionNode _run;
    private HealthComponent _playerHealth;
    private double _elapsed;
    private double _totalElapsed;
    private int _stage;
    private int _burnStartHealth;
    private int _pausedHealth;
    private string _pausedSummary;
    private double _pausedRemaining;
    private bool _frostbiteCast;
    private bool _combustionCast;
    private bool _overloadCast;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _arena = GetNode<TestArena3D>("Arena3D");
        _arena.ProcessMode = ProcessModeEnum.Pausable;
        _player = _arena.GetNode<PlayerController3D>("Player3D");
        _flow = _arena.GetNode<GameFlowController3D>("GameFlow3D");
        _run = _arena.GetNode<RunSessionNode>("RunSession");
        _playerHealth = _player.GetNode<HealthComponent>("HealthComponent");
        _player.SetPhysicsProcess(false);
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        _totalElapsed += delta;
        if (_totalElapsed > 20.0)
        {
            Fail($"timeout stage={_stage}");
            return;
        }

        try
        {
            switch (_stage)
            {
                case 0:
                    BindEnemy();
                    break;
                case 1:
                    VerifyProcAndIncomingDamage();
                    break;
                case 2:
                    VerifyFirstBurnTick();
                    break;
                case 3:
                    VerifyBurningLifecycle();
                    break;
                case 4:
                    VerifyPauseFreeze();
                    break;
                case 5:
                    VerifyDeathCleanup();
                    break;
                case 6:
                    VerifySupportRuntime();
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    private void BindEnemy()
    {
        var director = _arena.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        // This smoke intentionally runs the legacy TestArena fixture so it
        // can isolate ailment timing from the encounter director. Bind the
        // map-local fixture first, then retain the director fallback for
        // future runtime-scene variants.
        _feral ??= _arena.GetNodeOrNull<FeralController3D>("Feral3D");
        _feral ??= director?.GetActiveEnemies().OfType<FeralController3D>().FirstOrDefault();
        if (_feral == null)
        {
            return;
        }

        if (director != null)
        {
            director.ProcessMode = ProcessModeEnum.Disabled;
            foreach (var enemy in director.GetActiveEnemies())
            {
                enemy.SetPhysicsProcess(false);
            }
        }

        foreach (var enemy in _arena.GetChildren().OfType<Node3D>())
        {
            if (enemy is FeralController3D
                or SpitterController3D
                or BrimstoneColossusController3D)
            {
                enemy.SetPhysicsProcess(false);
            }
        }

        _stage = 1;
        _elapsed = 0.0;
    }

    private void VerifyProcAndIncomingDamage()
    {
        var eventBefore = _run.Session.EventRandom.State;
        var lootBefore = _run.Session.LootRandom.State;
        var craftingBefore = _run.Session.CraftingRandom.State;
        var noProc = _player.ApplyDamage(CreateAilmentRequest(
            1,
            AilmentKind.Burning,
            0,
            "ailment_zero_chance"));
        if (noProc.DamageApplied != 1
            || _player.Ailments.Collection.Has(AilmentKind.Burning)
            || _run.Session.EventRandom.State != eventBefore
            || _run.Session.LootRandom.State != lootBefore
            || _run.Session.CraftingRandom.State != craftingBefore)
        {
            Fail("0% ailment proc consumed RNG or applied a status");
            return;
        }

        _player.Ailments.ResetPresentation();
        _playerHealth.ResetHealth();
        var chilled = _player.ApplyDamage(CreateAilmentRequest(
            1,
            AilmentKind.Chilled,
            100,
            "ailment_chilled"));
        if (chilled.DamageApplied != 1
            || !_player.Ailments.Collection.Has(AilmentKind.Chilled)
            || _run.Session.EventRandom.State != eventBefore)
        {
            Fail("100% Chilled proc did not apply without consuming event RNG");
            return;
        }

        _player.Ailments.ResetPresentation();
        _playerHealth.ResetHealth();
        var shocked = _player.ApplyDamage(CreateAilmentRequest(
            10,
            AilmentKind.Shocked,
            100,
            "ailment_shocked"));
        var amplified = _player.ApplyDamage(new DamageRequest(
            10,
            DamageType.Physical,
            "ailment_incoming_multiplier",
            CombatFaction.Enemy));
        if (shocked.DamageApplied != 10
            || amplified.DamageApplied != 12
            || !_player.Ailments.Collection.Has(AilmentKind.Shocked))
        {
            Fail($"Shocked incoming damage mismatch direct={shocked.DamageApplied} amplified={amplified.DamageApplied}");
            return;
        }

        _player.Ailments.ResetPresentation();
        _playerHealth.ResetHealth();
        var burn = _player.ApplyDamage(CreateAilmentRequest(
            20,
            AilmentKind.Burning,
            100,
            "ailment_burning"));
        if (burn.DamageApplied != 20 || !_player.Ailments.Collection.Has(AilmentKind.Burning))
        {
            Fail("100% Burning proc did not apply after positive damage");
            return;
        }

        _burnStartHealth = _player.CurrentHealth;
        _stage = 2;
        _elapsed = 0.0;
    }

    private void VerifyFirstBurnTick()
    {
        if (_elapsed < 1.15)
        {
            return;
        }

        if (_burnStartHealth - _player.CurrentHealth != 5)
        {
            Fail($"Burning first tick mismatch damage={_burnStartHealth - _player.CurrentHealth}");
            return;
        }

        _stage = 3;
    }

    private void VerifyBurningLifecycle()
    {
        if (_elapsed < 3.35)
        {
            return;
        }

        if (_burnStartHealth - _player.CurrentHealth != 15
            || _player.Ailments.Collection.Has(AilmentKind.Burning))
        {
            Fail($"Burning lifecycle mismatch total={_burnStartHealth - _player.CurrentHealth} active={_player.Ailments.Collection.Has(AilmentKind.Burning)}");
            return;
        }

        _player.Ailments.ResetPresentation();
        _playerHealth.ResetHealth();
        _player.ApplyDamage(CreateAilmentRequest(4, AilmentKind.Burning, 100, "ailment_pause"));
        _pausedHealth = _player.CurrentHealth;
        _pausedSummary = _player.Ailments.Summary;
        _pausedRemaining = _player.Ailments.Collection.Get(AilmentKind.Burning)!.RemainingSeconds;
        _flow.RestoreState(GameFlowState.GameOver);
        _stage = 4;
        _elapsed = 0.0;
    }

    private void VerifyPauseFreeze()
    {
        if (_elapsed < 0.85)
        {
            return;
        }

        var current = _player.Ailments.Collection.Get(AilmentKind.Burning);
        if (!GetTree().Paused
            || _player.CurrentHealth != _pausedHealth
            || _player.Ailments.Summary != _pausedSummary
            || current == null
            || Math.Abs(current.RemainingSeconds - _pausedRemaining) > 0.001)
        {
            Fail("paused ailment advanced damage or duration");
            return;
        }

        _flow.RestoreState(GameFlowState.Playing);
        _player.Ailments.ResetPresentation();
        _playerHealth.ResetHealth();
        _stage = 5;
        _elapsed = 0.0;
    }

    private void VerifyDeathCleanup()
    {
        _feral.SetPhysicsProcess(false);
        _feral.ApplyDamage(CreateAilmentRequest(1, AilmentKind.Chilled, 100, "ailment_death_status"));
        if (!_feral.Ailments.Collection.Has(AilmentKind.Chilled))
        {
            Fail("Feral did not receive pre-death Chilled status");
            return;
        }

        _feral.ApplyDamage(new DamageRequest(
            9999,
            DamageType.Physical,
            "ailment_death",
            CombatFaction.Player));
        if (_feral.IsAlive
            || _feral.Ailments.Collection.Active.Count != 0
            || _feral.CollisionLayer != 0
            || _feral.CollisionMask != 0)
        {
            Fail("death did not clear ailments and disable Feral runtime");
            return;
        }

        CreateSupportFeral();
        _stage = 6;
        _elapsed = 0.0;
    }

    private void CreateSupportFeral()
    {
        var scene = GD.Load<PackedScene>("res://scenes3d/Feral3D.tscn")
            ?? throw new InvalidOperationException("could not load Feral scene for support runtime");
        _supportFeral = scene.Instantiate<FeralController3D>();
        _supportFeral.ForceGuaranteedDropForTest = true;
        _arena.GetNode<Node3D>("EnemyContainer").AddChild(_supportFeral);
        _supportFeral.GlobalPosition = new Vector3(2.5f, 0.0f, 0.0f);
        _player.GlobalPosition = Vector3.Zero;
        _player.SetAimDirectionForTest(Vector3.Right);
    }

    private void VerifySupportRuntime()
    {
        if (_supportFeral == null || !GodotObject.IsInstanceValid(_supportFeral) || _elapsed < 0.15)
        {
            return;
        }

        _supportFeral.SetPhysicsProcess(false);
        if (!_frostbiteCast)
        {
            PositionEventRandomForSuccessfulProc(35);
            if (!_player.Skills.TryDetachSupport(SkillSlot.Primary)
                || !_player.Skills.TryAttachSupport(SkillSlot.Primary, "frostbite")
                || !_player.Skills.TryCastForTest(SkillSlot.Primary))
            {
                Fail("Frostbite support could not be attached and cast");
                return;
            }

            _frostbiteCast = true;
        }

        if (_elapsed < 0.65)
        {
            return;
        }

        if (!_supportFeral.Ailments.Collection.Has(AilmentKind.Chilled))
        {
            Fail("Frostbite runtime cast did not apply Chilled");
            return;
        }

        if (!_combustionCast
            && (!_player.Skills.TryDetachSupport(SkillSlot.Secondary)
                || !_player.Skills.TryAttachSupport(SkillSlot.Secondary, "combustion")
                || !_player.Skills.TryCastAreaAtForTest(SkillSlot.Secondary, _supportFeral.GlobalPosition)))
        {
            Fail("Combustion support could not be attached and cast");
            return;
        }

        _combustionCast = true;

        if (_elapsed < 1.35)
        {
            return;
        }

        if (!_supportFeral.Ailments.Collection.Has(AilmentKind.Burning))
        {
            Fail("Combustion runtime cast did not apply Burning");
            return;
        }

        if (!_overloadCast)
        {
            PositionEventRandomForSuccessfulProc(50);
            if (!_player.Skills.TryAttachSupport(SkillSlot.Utility, "overload")
                || !_player.Skills.TryCastAreaAtForTest(SkillSlot.Utility, _player.GlobalPosition))
            {
                Fail("Overload support could not be attached and cast");
                return;
            }

            _overloadCast = true;
        }

        if (_elapsed < 1.55)
        {
            return;
        }

        if (!_supportFeral.Ailments.Collection.Has(AilmentKind.Shocked)
            || _player.Skills.CooldownRemaining(SkillSlot.Utility) <= 2.0f)
        {
            Fail("Overload runtime cast did not apply Shocked or cooldown multiplier");
            return;
        }

        GD.Print("AILMENT_RUNTIME_3D_REGRESSION_PASS proc=true rng_boundary=true shocked=true burning_ticks=3 pause=true death_clear=true supports=true");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GetTree().Quit(0);
    }

    private void PositionEventRandomForSuccessfulProc(int chancePercent)
    {
        var baseline = _run.Session.EventRandom.State;
        for (var offset = 1UL; offset < 4096UL; offset++)
        {
            var candidate = RandomService.DeriveSeed(baseline, offset);
            var probe = new RandomService(candidate);
            if (probe.Chance(chancePercent))
            {
                // The actual skill cast still consumes EventRandom exactly
                // once. The probe only chooses a deterministic fixture state
                // so a probabilistic support smoke cannot flake.
                _run.Session.EventRandom.RestoreState(candidate);
                return;
            }
        }

        throw new InvalidOperationException("could not find a deterministic ailment proc fixture");
    }

    private static DamageRequest CreateAilmentRequest(
        int rawDamage,
        AilmentKind kind,
        int chancePercent,
        string sourceId)
    {
        var damageType = kind switch
        {
            AilmentKind.Burning => DamageType.Fire,
            AilmentKind.Chilled => DamageType.Cold,
            AilmentKind.Shocked => DamageType.Lightning,
            _ => DamageType.Physical,
        };
        return new DamageRequest(
            rawDamage,
            damageType,
            sourceId,
            CombatFaction.Enemy,
            false,
            new AilmentApplicationDefinition(kind, chancePercent));
    }

    private void Fail(string message)
    {
        GD.PushError($"AILMENT_RUNTIME_3D_REGRESSION_FAIL {message}");
        GetTree().Quit(1);
    }
}
