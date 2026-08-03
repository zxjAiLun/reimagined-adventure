using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// Regression smoke for replacement semantics and transient-state lifecycle:
/// stronger effects replace, weaker effects preserve potency, pause freezes,
/// death clears, and save/load rebuilds a clean temporary-status state.
/// </summary>
public partial class AilmentLifecycle3DRegressionSmoke : Node
{
    private TestArena3D _arena;
    private PlayerController3D _player;
    private FeralController3D _feral;
    private GameFlowController3D _flow;
    private SaveBoundaryNode3D _save;
    private HealthComponent _feralHealth;
    private double _elapsed;
    private double _totalElapsed;
    private int _stage;
    private double _pausedRemaining;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _arena = GetNode<TestArena3D>("Arena3D");
        _arena.ProcessMode = ProcessModeEnum.Pausable;
        _player = _arena.GetNode<PlayerController3D>("Player3D");
        _flow = _arena.GetNode<GameFlowController3D>("GameFlow3D");
        _save = _arena.GetNode<SaveBoundaryNode3D>("SaveBoundary3D");
        _arena.GetNode<EncounterDirector3D>("EncounterDirector3D").ProcessMode = ProcessModeEnum.Disabled;
        CreateFeral();
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        _totalElapsed += delta;
        if (_totalElapsed > 15.0)
        {
            Fail($"timeout stage={_stage}");
            return;
        }

        try
        {
            switch (_stage)
            {
                case 0:
                    VerifyReplacementSemantics();
                    break;
                case 1:
                    VerifyPauseAndDeath();
                    break;
                case 2:
                    VerifySaveStartsClean();
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    private void CreateFeral()
    {
        var scene = GD.Load<PackedScene>("res://scenes3d/Feral3D.tscn")
            ?? throw new InvalidOperationException("could not load Feral scene");
        _feral = scene.Instantiate<FeralController3D>();
        _feral.ForceGuaranteedDropForTest = true;
        _arena.GetNode<Node3D>("EnemyContainer").AddChild(_feral);
        _feral.GlobalPosition = new Vector3(2.0f, 0.0f, 0.0f);
        _feralHealth = _feral.GetNode<HealthComponent>("HealthComponent");
        _feral.SetPhysicsProcess(false);
    }

    private void VerifyReplacementSemantics()
    {
        if (!GodotObject.IsInstanceValid(_feral) || !_feral.IsInsideTree())
        {
            return;
        }

        var weak = _feral.ApplyDamage(CreateRequest(8, AilmentKind.Burning, "lifecycle_weak"));
        if (weak.DamageApplied != 8
            || !_feral.Ailments.Collection.Has(AilmentKind.Burning)
            || _feral.Ailments.Collection.Get(AilmentKind.Burning)!.Potency != 2)
        {
            Fail("initial Burning application was not created");
            return;
        }

        var weaker = _feral.ApplyDamage(CreateRequest(2, AilmentKind.Burning, "lifecycle_weaker"));
        var preserved = _feral.Ailments.Collection.Get(AilmentKind.Burning);
        if (weaker.DamageApplied != 2
            || preserved == null
            || preserved.Potency != 2
            || preserved.SourceId != "lifecycle_weak")
        {
            Fail("weaker Burning did not preserve potency and source");
            return;
        }

        _feralHealth.ResetHealth();
        _feral.Ailments.ResetPresentation();
        _feral.ApplyDamage(CreateRequest(8, AilmentKind.Burning, "lifecycle_first"));
        _feral.ApplyDamage(CreateRequest(16, AilmentKind.Burning, "lifecycle_stronger"));
        var replaced = _feral.Ailments.Collection.Get(AilmentKind.Burning);
        if (replaced == null
            || replaced.Potency != 4
            || replaced.SourceId != "lifecycle_stronger")
        {
            Fail("stronger Burning did not replace potency and source");
            return;
        }

        _feral.Ailments.ResetPresentation();
        _feralHealth.ResetHealth();
        _feral.ApplyDamage(CreateRequest(1, AilmentKind.Chilled, "lifecycle_pause"));
        _pausedRemaining = _feral.Ailments.Collection.Get(AilmentKind.Chilled)!.RemainingSeconds;
        _flow.RestoreState(GameFlowState.GameOver);
        _stage = 1;
        _elapsed = 0.0;
    }

    private void VerifyPauseAndDeath()
    {
        if (_elapsed < 0.85)
        {
            return;
        }

        var chilled = _feral.Ailments.Collection.Get(AilmentKind.Chilled);
        if (!GetTree().Paused
            || chilled == null
            || Math.Abs(chilled.RemainingSeconds - _pausedRemaining) > 0.001)
        {
            Fail("paused Chilled status advanced");
            return;
        }

        _flow.RestoreState(GameFlowState.Playing);
        _feral.Ailments.ResetPresentation();
        _feralHealth.ResetHealth();
        _feral.ApplyDamage(CreateRequest(1, AilmentKind.Shocked, "lifecycle_death_status"));
        _feral.ApplyDamage(new DamageRequest(
            9999,
            DamageType.Physical,
            "lifecycle_death",
            CombatFaction.Player));
        if (_feral.IsAlive || _feral.Ailments.Collection.Active.Count != 0)
        {
            Fail("death left a transient ailment active");
            return;
        }

        _player.Ailments.ResetPresentation();
        _player.GetNode<HealthComponent>("HealthComponent").ResetHealth();
        _player.ApplyDamage(CreateRequest(1, AilmentKind.Chilled, "lifecycle_save_status"));
        if (!_save.TrySaveCurrentRun(out var saveError))
        {
            Fail($"could not save transient-state smoke: {saveError}");
            return;
        }

        _player.Ailments.ResetPresentation();
        _stage = 2;
        _elapsed = 0.0;
    }

    private void VerifySaveStartsClean()
    {
        if (_elapsed < 0.15)
        {
            return;
        }

        if (!_save.TryLoadAndApplyLastRun(out _, out var error))
        {
            Fail($"transient-state save recovery failed: {error}");
            return;
        }

        if (_player.Ailments.Collection.Active.Count != 0)
        {
            Fail("save recovery restored a transient ailment");
            return;
        }

        GD.Print("AILMENT_LIFECYCLE_3D_REGRESSION_PASS stronger_replaces=true weaker_preserves=true pause=true death_clear=true save_clean=true");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GetTree().Quit(0);
    }

    private static DamageRequest CreateRequest(int rawDamage, AilmentKind kind, string sourceId)
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
            CombatFaction.Player,
            false,
            new AilmentApplicationDefinition(kind, 100));
    }

    private void Fail(string message)
    {
        GD.PushError($"AILMENT_LIFECYCLE_3D_REGRESSION_FAIL {message}");
        GetTree().Quit(1);
    }
}
