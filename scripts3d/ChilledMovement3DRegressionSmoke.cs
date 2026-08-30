using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// Measures real navigation displacement for normal and Chilled Feral and
/// Spitter actors, including approach, retreat, elite ordering, expiry and
/// pause behavior.
/// </summary>
public partial class ChilledMovement3DRegressionSmoke : Node
{
    private enum Stage
    {
        WaitReady,
        FeralNormal,
        FeralChilled,
        FeralExpire,
        FeralRecovery,
        Pause,
        SpitterApproachNormal,
        SpitterApproachChilled,
        SpitterRetreatPrepare,
        SpitterRetreatNormal,
        SpitterRetreatChilled,
        EliteNormal,
        EliteChilled,
        BossNormal,
        BossChilled,
    }

    private Stage _stage;
    private double _elapsed;
    private double _stageElapsed;
    private bool _complete;
    private TestArena3D _arena;
    private PlayerController3D _player;
    private FeralController3D _feral;
    private SpitterController3D _spitter;
    private FeralController3D _elite;
    private BrimstoneColossusController3D _boss;
    private Vector3 _startPosition;
    private float _feralNormalDisplacement;
    private float _feralChilledDisplacement;
    private float _spitterApproachNormalDisplacement;
    private float _spitterApproachChilledDisplacement;
    private float _spitterRetreatNormalDisplacement;
    private float _spitterRetreatChilledDisplacement;
    private float _eliteNormalDisplacement;
    private float _eliteChilledDisplacement;
    private float _bossNormalDisplacement;
    private float _bossChilledDisplacement;

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
        if (_elapsed > 38.0)
        {
            Fail($"Chilled movement smoke timed out at stage {_stage}");
            return;
        }

        try
        {
            _arena ??= GetNodeOrNull<TestArena3D>("Arena3D");
            _player ??= _arena?.GetNodeOrNull<PlayerController3D>("Player3D");
            _feral ??= _arena?.GetNodeOrNull<FeralController3D>("Feral3D");
            _spitter ??= _arena?.GetNodeOrNull<SpitterController3D>("Spitter3D");
            _boss ??= _arena?.GetNodeOrNull<BrimstoneColossusController3D>("BrimstoneColossus3D");

            switch (_stage)
            {
                case Stage.WaitReady:
                    PrepareActors();
                    break;
                case Stage.FeralNormal:
                    MeasureFeralNormal();
                    break;
                case Stage.FeralChilled:
                    MeasureFeralChilled();
                    break;
                case Stage.FeralExpire:
                    WaitForChilledExpiry();
                    break;
                case Stage.FeralRecovery:
                    MeasureFeralRecovery();
                    break;
                case Stage.Pause:
                    VerifyPause();
                    break;
                case Stage.SpitterApproachNormal:
                    MeasureSpitterApproachNormal();
                    break;
                case Stage.SpitterApproachChilled:
                    MeasureSpitterApproachChilled();
                    break;
                case Stage.SpitterRetreatPrepare:
                    PrepareSpitterRetreat();
                    break;
                case Stage.SpitterRetreatNormal:
                    MeasureSpitterRetreatNormal();
                    break;
                case Stage.SpitterRetreatChilled:
                    MeasureSpitterRetreatChilled();
                    break;
                case Stage.EliteNormal:
                    MeasureEliteNormal();
                    break;
                case Stage.EliteChilled:
                    MeasureEliteChilled();
                    break;
                case Stage.BossNormal:
                    MeasureBossNormal();
                    break;
                case Stage.BossChilled:
                    MeasureBossChilled();
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    private void PrepareActors()
    {
        if (_arena == null || _player == null || _feral == null || _spitter == null)
        {
            return;
        }

        _arena.ProcessMode = ProcessModeEnum.Pausable;
        var boss = _arena.GetNodeOrNull<BrimstoneColossusController3D>("BrimstoneColossus3D");
        boss?.SetPhysicsProcess(false);
        _spitter.SetPhysicsProcess(false);
        _player.GlobalPosition = Vector3.Zero;
        _feral.ResetForNavigationPressureTest(new Vector3(7.0f, 0.0f, 0.0f));
        _feral.Ailments.ResetPresentation();
        _spitter.GlobalPosition = new Vector3(8.0f, 0.0f, 0.0f);
        _spitter.Navigation?.Stop();
        _stage = Stage.FeralNormal;
        _stageElapsed = 0.0;
        _startPosition = _feral.GlobalPosition;
    }

    private void MeasureFeralNormal()
    {
        if (_stageElapsed < 0.75)
        {
            return;
        }

        _feralNormalDisplacement = HorizontalDistance(_startPosition, _feral.GlobalPosition);
        ResetFeralForMeasurement();
        var result = _feral.ApplyDamage(ChillRequest("feral_chilled_measurement"));
        if (result.DamageApplied <= 0 || !_feral.Ailments.Collection.Has(AilmentKind.Chilled))
        {
            Fail("Feral Chilled was not applied for movement measurement");
            return;
        }

        _stage = Stage.FeralChilled;
        _stageElapsed = 0.0;
        _startPosition = _feral.GlobalPosition;
    }

    private void MeasureFeralChilled()
    {
        if (_stageElapsed < 0.75)
        {
            return;
        }

        _feralChilledDisplacement = HorizontalDistance(_startPosition, _feral.GlobalPosition);
        AssertChilledRatio(
            "Feral",
            _feralNormalDisplacement,
            _feralChilledDisplacement);
        _player.GlobalPosition = new Vector3(-12.0f, 0.0f, 0.0f);
        _stage = Stage.FeralExpire;
        _stageElapsed = 0.0;
    }

    private void WaitForChilledExpiry()
    {
        if (_stageElapsed < AilmentCollection.ChilledDurationSeconds + 0.25)
        {
            return;
        }

        if (_feral.Ailments.Collection.Has(AilmentKind.Chilled))
        {
            Fail("Feral Chilled did not expire");
            return;
        }

        _player.GlobalPosition = Vector3.Zero;
        ResetFeralForMeasurement();
        _stage = Stage.FeralRecovery;
        _stageElapsed = 0.0;
        _startPosition = _feral.GlobalPosition;
    }

    private void MeasureFeralRecovery()
    {
        if (_stageElapsed < 0.75)
        {
            return;
        }

        var recovered = HorizontalDistance(_startPosition, _feral.GlobalPosition);
        if (recovered < _feralNormalDisplacement * 0.75f)
        {
            Fail($"Feral did not recover movement speed normal={_feralNormalDisplacement:0.000} recovered={recovered:0.000}");
            return;
        }

        _feral.SetPhysicsProcess(false);
        _player.GlobalPosition = Vector3.Zero;
        _feral.GlobalPosition = new Vector3(7.0f, 0.0f, 0.0f);
        _stage = Stage.Pause;
        _stageElapsed = 0.0;
        _startPosition = _feral.GlobalPosition;
        _feral.SetPhysicsProcess(true);
        GetTree().Paused = true;
    }

    private void VerifyPause()
    {
        if (_stageElapsed < 0.75)
        {
            return;
        }

        var pausedDisplacement = HorizontalDistance(_startPosition, _feral.GlobalPosition);
        GetTree().Paused = false;
        if (pausedDisplacement > 0.01f)
        {
            Fail($"paused Feral moved {pausedDisplacement:0.000}");
            return;
        }

        _spitter.SetPhysicsProcess(true);
        _player.GlobalPosition = Vector3.Zero;
        _spitter.GlobalPosition = new Vector3(8.0f, 0.0f, 0.0f);
        _spitter.Navigation?.Stop();
        _spitter.Ailments.ResetPresentation();
        _stage = Stage.SpitterApproachNormal;
        _stageElapsed = 0.0;
        _startPosition = _spitter.GlobalPosition;
    }

    private void MeasureSpitterApproachNormal()
    {
        if (_stageElapsed < 0.75)
        {
            return;
        }

        _spitterApproachNormalDisplacement = HorizontalDistance(_startPosition, _spitter.GlobalPosition);
        ResetSpitterForMeasurement(new Vector3(8.0f, 0.0f, 0.0f));
        var result = _spitter.ApplyDamage(ChillRequest("spitter_approach_chilled_measurement"));
        if (result.DamageApplied <= 0 || !_spitter.Ailments.Collection.Has(AilmentKind.Chilled))
        {
            Fail("Spitter approach Chilled was not applied");
            return;
        }

        _stage = Stage.SpitterApproachChilled;
        _stageElapsed = 0.0;
        _startPosition = _spitter.GlobalPosition;
    }

    private void MeasureSpitterApproachChilled()
    {
        if (_stageElapsed < 0.75)
        {
            return;
        }

        _spitterApproachChilledDisplacement = HorizontalDistance(_startPosition, _spitter.GlobalPosition);
        AssertChilledRatio(
            "Spitter approach",
            _spitterApproachNormalDisplacement,
            _spitterApproachChilledDisplacement);
        ResetSpitterForMeasurement(new Vector3(2.0f, 0.0f, 0.0f));
        _spitter.Ailments.ResetPresentation();
        _stage = Stage.SpitterRetreatPrepare;
        _stageElapsed = 0.0;
    }

    private void PrepareSpitterRetreat()
    {
        if (_spitter.State != SpitterState3D.Retreating && _stageElapsed < 0.6)
        {
            return;
        }

        if (_spitter.State != SpitterState3D.Retreating)
        {
            Fail("Spitter did not enter Retreating for movement measurement");
            return;
        }

        _stage = Stage.SpitterRetreatNormal;
        _stageElapsed = 0.0;
        _startPosition = _spitter.GlobalPosition;
    }

    private void MeasureSpitterRetreatNormal()
    {
        // Keep the sample below the distance-band boundary so normal and
        // Chilled runs both remain in the same Retreating state.
        if (_stageElapsed < 0.40)
        {
            return;
        }

        _spitterRetreatNormalDisplacement = HorizontalDistance(_startPosition, _spitter.GlobalPosition);
        ResetSpitterForMeasurement(new Vector3(2.0f, 0.0f, 0.0f));
        _spitter.Ailments.ResetPresentation();
        _spitter.ApplyDamage(ChillRequest("spitter_retreat_chilled_measurement"));
        _stage = Stage.SpitterRetreatChilled;
        _stageElapsed = 0.0;
        _startPosition = _spitter.GlobalPosition;
    }

    private void MeasureSpitterRetreatChilled()
    {
        if (_stageElapsed < 0.40)
        {
            return;
        }

        _spitterRetreatChilledDisplacement = HorizontalDistance(_startPosition, _spitter.GlobalPosition);
        AssertChilledRatio(
            "Spitter retreat",
            _spitterRetreatNormalDisplacement,
            _spitterRetreatChilledDisplacement);
        CreateEliteFeral();
        _stage = Stage.EliteNormal;
        _stageElapsed = 0.0;
        _startPosition = _elite.GlobalPosition;
    }

    private void CreateEliteFeral()
    {
        var run = _arena.GetNode<RunSessionNode>("RunSession");
        run.Session.SetMapLevel(2);
        run.MapLevel = 2;
        var scene = GD.Load<PackedScene>("res://scenes3d/Feral3D.tscn");
        _elite = scene.Instantiate<FeralController3D>();
        _elite.Name = "EliteFeral3D";
        var context = new EnemySpawnContext3D(
            run,
            _player,
            2,
            "chilled_movement_smoke",
            "elite_measurement",
            99,
            1,
            new MapModifierStats(),
            2,
            false)
        {
            EliteModifier = EliteModifierLibrary.Find("frenzied"),
            EliteSelectionSeed = 0xE117EUL,
        };
        _elite.ConfigureBeforeReady(context);
        _arena.AddChild(_elite);
        _elite.GlobalPosition = new Vector3(7.0f, 0.0f, 0.0f);
    }

    private void MeasureEliteNormal()
    {
        if (_elite == null || !_elite.IsInsideTree() || _stageElapsed < 0.75)
        {
            return;
        }

        if (_elite.EliteModifierId != "frenzied" || _elite.EliteAppliedCount != 1)
        {
            Fail("elite movement fixture did not apply exactly once");
            return;
        }

        _eliteNormalDisplacement = HorizontalDistance(_startPosition, _elite.GlobalPosition);
        _elite.GlobalPosition = new Vector3(7.0f, 0.0f, 0.0f);
        _elite.Navigation?.Stop();
        var result = _elite.ApplyDamage(ChillRequest("elite_chilled_measurement"));
        if (result.DamageApplied <= 0 || !_elite.Ailments.Collection.Has(AilmentKind.Chilled))
        {
            Fail("elite Chilled was not applied");
            return;
        }

        _stage = Stage.EliteChilled;
        _stageElapsed = 0.0;
        _startPosition = _elite.GlobalPosition;
    }

    private void MeasureEliteChilled()
    {
        if (_stageElapsed < 0.75)
        {
            return;
        }

        _eliteChilledDisplacement = HorizontalDistance(_startPosition, _elite.GlobalPosition);
        AssertChilledRatio("Frenzied elite", _eliteNormalDisplacement, _eliteChilledDisplacement);
        if (_boss == null)
        {
            Fail("legacy Boss fixture was not available for Phase 1 movement measurement");
            return;
        }

        _player.GlobalPosition = new Vector3(7.0f, 0.0f, 5.0f);
        _boss.GlobalPosition = new Vector3(-7.0f, 0.0f, -5.0f);
        _boss.Ailments.ResetPresentation();
        _boss.Navigation?.Stop();
        _boss.SetPhysicsProcess(true);
        _stage = Stage.BossNormal;
        _stageElapsed = 0.0;
        _startPosition = _boss.GlobalPosition;
    }

    private void MeasureBossNormal()
    {
        if (_stageElapsed < 0.75)
        {
            return;
        }

        _bossNormalDisplacement = HorizontalDistance(_startPosition, _boss.GlobalPosition);
        _boss.GlobalPosition = new Vector3(-7.0f, 0.0f, -5.0f);
        _boss.Navigation?.Stop();
        var result = _boss.ApplyDamage(ChillRequest("boss_phase_one_chilled_movement"));
        if (result.DamageApplied <= 0
            || !_boss.Ailments.Collection.Has(AilmentKind.Chilled))
        {
            Fail("Boss Phase 1 Chilled was not applied for movement measurement");
            return;
        }

        _stage = Stage.BossChilled;
        _stageElapsed = 0.0;
        _startPosition = _boss.GlobalPosition;
    }

    private void MeasureBossChilled()
    {
        if (_stageElapsed < 0.75)
        {
            return;
        }

        _bossChilledDisplacement = HorizontalDistance(_startPosition, _boss.GlobalPosition);
        AssertChilledRatio("Boss Phase 1", _bossNormalDisplacement, _bossChilledDisplacement);
        _complete = true;
        GD.Print(
            $"CHILLED_MOVEMENT_3D_REGRESSION_PASS feral={_feralChilledDisplacement / _feralNormalDisplacement:0.00} "
            + $"spitter_approach={_spitterApproachChilledDisplacement / _spitterApproachNormalDisplacement:0.00} "
            + $"spitter_retreat={_spitterRetreatChilledDisplacement / _spitterRetreatNormalDisplacement:0.00} "
            + $"elite={_eliteChilledDisplacement / _eliteNormalDisplacement:0.00} "
            + $"boss_phase1={_bossChilledDisplacement / _bossNormalDisplacement:0.00} pause=true expiry=true");
        GetTree().Quit();
    }

    private void ResetFeralForMeasurement()
    {
        _feral.ResetForNavigationPressureTest(new Vector3(7.0f, 0.0f, 0.0f));
        _feral.Ailments.ResetPresentation();
    }

    private void ResetSpitterForMeasurement(Vector3 position)
    {
        _spitter.GlobalPosition = position;
        _spitter.Navigation?.Stop();
    }

    private static DamageRequest ChillRequest(string sourceId) => new(
        1,
        DamageType.Physical,
        sourceId,
        CombatFaction.Player,
        false,
        new AilmentApplicationDefinition(AilmentKind.Chilled, 100));

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        var delta = first - second;
        delta.Y = 0.0f;
        return delta.Length();
    }

    private void AssertChilledRatio(string actor, float normal, float chilled)
    {
        if (normal < 0.1f || chilled < 0.1f)
        {
            throw new InvalidOperationException($"{actor} did not produce measurable navigation displacement normal={normal:0.000} chilled={chilled:0.000}");
        }

        var ratio = chilled / normal;
        if (Math.Abs(ratio - 0.80f) > 0.16f)
        {
            throw new InvalidOperationException($"{actor} Chilled movement ratio was {ratio:0.000} normal={normal:0.000} chilled={chilled:0.000}");
        }
    }

    private void Fail(string reason)
    {
        if (_complete)
        {
            return;
        }

        _complete = true;
        GetTree().Paused = false;
        GD.PushError($"CHILLED_MOVEMENT_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}
