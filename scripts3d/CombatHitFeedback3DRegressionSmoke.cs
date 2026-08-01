using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// Regression smoke for the Stage 2C feedback contracts. It observes the
/// presentation nodes while all damage still comes from the normal 3D actor
/// and projectile paths.
/// </summary>
public partial class CombatHitFeedback3DRegressionSmoke : Node
{
    private TestArena3D _arena;
    private PlayerController3D _player;
    private FeralController3D _feral;
    private SpitterController3D _spitter;
    private BrimstoneColossusController3D _boss;
    private GameFlowController3D _flow;
    private DamageNumberSpawner3D _numbers;
    private HitFlash3D _feralFlash;
    private DeathFeedback3D _feralDeath;
    private FeralController3D _dynamicFeral;
    private int _stage;
    private double _stageElapsed;
    private double _totalElapsed;
    private int _numbersBefore;
    private int _multiNumbersBefore;
    private int _multiHealthBefore;
    private int _pausedFeralHealth;
    private Vector3 _pausedNumberPosition;
    private float _pausedNumberElapsed;
    private float _pausedFlashProgress;
    private DamageNumber3D _pausedNumber;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _arena = GetNode<TestArena3D>("Arena3D");
        _arena.ProcessMode = ProcessModeEnum.Pausable;
        _player = _arena.GetNode<PlayerController3D>("Player3D");
        _feral = _arena.GetNode<FeralController3D>("Feral3D");
        _spitter = _arena.GetNode<SpitterController3D>("Spitter3D");
        _boss = _arena.GetNode<BrimstoneColossusController3D>("BrimstoneColossus3D");
        _feral.ForceGuaranteedDropForTest = true;
        _flow = _arena.GetNode<GameFlowController3D>("GameFlow3D");
        _numbers = _arena.GetNode<DamageNumberSpawner3D>("DamageNumberSpawner3D");

        var fallbackSession = _arena.GetNode<RunSessionNode>("RunSession");
        if (!ReferenceEquals(_feral.RunSession, fallbackSession)
            || !ReferenceEquals(_spitter.RunSession, fallbackSession)
            || !ReferenceEquals(_boss.RunSession, fallbackSession)
            || !ReferenceEquals(_feral.TargetPlayer, _player)
            || !ReferenceEquals(_spitter.TargetPlayer, _player)
            || !ReferenceEquals(_boss.TargetPlayer, _player))
        {
            Fail("independent TestArena actors did not use the local fallback RunSession and Player");
            return;
        }

        _feralFlash = _feral.GetNode<HitFlash3D>("HitFlash3D");
        _feralDeath = _feral.GetNode<DeathFeedback3D>("DeathFeedback3D");

        _feral.SetPhysicsProcess(false);
        _spitter.SetPhysicsProcess(false);
        _boss.SetPhysicsProcess(false);
        CallDeferred(nameof(BeginSmoke));
    }

    public override void _Process(double delta)
    {
        _totalElapsed += delta;
        _stageElapsed += delta;
        if (_totalElapsed > 20.0)
        {
            Fail($"timeout stage={_stage}");
            return;
        }

        switch (_stage)
        {
            case 0:
                StartDamageNumberTest();
                break;
            case 1:
                VerifyDamageNumberAndZeroDamage();
                break;
            case 2:
                StartPauseTest();
                break;
            case 3:
                VerifyPauseFreeze();
                break;
            case 4:
                StartSpreadTest();
                break;
            case 5:
                VerifySpreadNumbers();
                break;
            case 6:
                StartDeathTest();
                break;
            case 7:
                VerifyDeathFeedback();
                break;
            case 8:
                StartDynamicFeralTest();
                break;
            case 9:
                VerifyDynamicFeralFeedback();
                break;
            case 10:
                VerifyBossMapComplete();
                break;
        }
    }

    private void BeginSmoke()
    {
        _flow.RestoreState(GameFlowState.Playing);
        _stage = 0;
        _stageElapsed = 0.0;
    }

    private void StartDamageNumberTest()
    {
        if (_numbers == null || _numbers.LastSpawnedNumber != null)
        {
            // A fresh arena should not contain presentation numbers. If it
            // does, wait one frame for the deferred source binding instead of
            // treating that setup frame as a combat result.
            if (_stageElapsed < 0.5)
            {
                return;
            }

            Fail("damage number spawner was not ready or was not empty");
            return;
        }

        _feral.GlobalPosition = new Vector3(3.0f, 0.0f, 0.0f);
        var result = _feral.ApplyDamage(new DamageRequest(
            5,
            DamageType.Physical,
            "combat_hit_feedback_5",
            CombatFaction.Player));
        if (result.DamageApplied != 5)
        {
            Fail($"expected 5 damage, got {result.DamageApplied}");
            return;
        }

        _numbersBefore = _numbers.SpawnedCount;
        if (_numbers.LastSpawnedText != "5"
            || _numbers.LastSpawnedNumber == null
            || !_feralFlash.IsActive
            || !_feralFlash.HasIsolatedMaterial)
        {
            Fail($"damage result did not immediately activate feedback count={_numbers.SpawnedCount} text={_numbers.LastSpawnedText} flash={_feralFlash.IsActive} material={_feralFlash.HasIsolatedMaterial}");
            return;
        }

        var countBeforeZero = _numbers.SpawnedCount;
        var zeroResult = _spitter.ApplyDamage(new DamageRequest(
            0,
            DamageType.Physical,
            "combat_hit_feedback_zero",
            CombatFaction.Player));
        if (zeroResult.DamageApplied != 0 || _numbers.SpawnedCount != countBeforeZero)
        {
            Fail("zero damage generated a presentation number");
            return;
        }

        StartPauseTest();
    }

    private void VerifyDamageNumberAndZeroDamage()
    {
        if (_numbers.SpawnedCount != _numbersBefore
            || _numbers.LastSpawnedText != "5"
            || _numbers.LastSpawnedNumber == null
            || !_feralFlash.IsActive
            || !_feralFlash.HasIsolatedMaterial)
        {
            Fail($"damage number or hit flash missing count={_numbers.SpawnedCount} text={_numbers.LastSpawnedText} flash={_feralFlash.IsActive} material={_feralFlash.HasIsolatedMaterial}");
            return;
        }

        _stage = 2;
        _stageElapsed = 0.0;
    }

    private void StartPauseTest()
    {
        _pausedNumber = _numbers.LastSpawnedNumber;
        if (_pausedNumber == null || !GodotObject.IsInstanceValid(_pausedNumber))
        {
            Fail("damage number disappeared before pause test");
            return;
        }

        _pausedNumberPosition = _pausedNumber.GlobalPosition;
        _pausedNumberElapsed = _pausedNumber.ElapsedSeconds;
        _pausedFlashProgress = _feralFlash.Progress;
        _pausedFeralHealth = _player.CurrentHealth;
        _flow.RestoreState(GameFlowState.GameOver);
        _stage = 3;
        _stageElapsed = 0.0;
    }

    private void VerifyPauseFreeze()
    {
        if (_stageElapsed < 0.8)
        {
            return;
        }

        if (!GetTree().Paused
            || !GodotObject.IsInstanceValid(_pausedNumber)
            || _pausedNumber.GlobalPosition.DistanceTo(_pausedNumberPosition) > 0.001f
            || Mathf.Abs(_pausedNumber.ElapsedSeconds - _pausedNumberElapsed) > 0.001f
            || Mathf.Abs(_feralFlash.Progress - _pausedFlashProgress) > 0.001f
            || _player.CurrentHealth != _pausedFeralHealth)
        {
            Fail("pause advanced damage number, flash, or gameplay state");
            return;
        }

        _flow.RestoreState(GameFlowState.Playing);
        _stage = 4;
        _stageElapsed = 0.0;
    }

    private void StartSpreadTest()
    {
        _feral.GetNode<HealthComponent>("HealthComponent").ResetHealth();
        _player.GlobalPosition = Vector3.Zero;
        _player.SetAimDirectionForTest(Vector3.Right);
        _feral.GlobalPosition = Vector3.Right.Rotated(Vector3.Up, Mathf.DegToRad(12.0f)) * 2.5f;
        _multiNumbersBefore = _numbers.SpawnedCount;
        _multiHealthBefore = _feral.CurrentHealth;
        var spreadSkill = new SkillDefinition
        {
            Id = "combat_hit_feedback_spread",
            Name = "Spread Shot Smoke",
            Slot = SkillSlot.Primary,
            CastType = SkillCastType.Projectile,
            BaseDamage = 2,
            ProjectileCount = 3,
            SpreadAngleDegrees = 36.0,
            DamageType = DamageType.Physical,
        };
        if (!_player.CastSpreadShot(spreadSkill, Array.Empty<SupportDefinition>()))
        {
            Fail("Spread Shot could not launch for multi-hit smoke");
            return;
        }

        _stage = 5;
        _stageElapsed = 0.0;
    }

    private void VerifySpreadNumbers()
    {
        if (_stageElapsed < 0.6)
        {
            return;
        }

        var numberDelta = _numbers.SpawnedCount - _multiNumbersBefore;
        if (numberDelta != 2 || _feral.CurrentHealth >= _multiHealthBefore)
        {
            Fail($"Spread Shot did not produce two independent hit numbers count={numberDelta} hp={_feral.CurrentHealth}/{_multiHealthBefore}");
            return;
        }

        _stage = 6;
        _stageElapsed = 0.0;
    }

    private void StartDeathTest()
    {
        _feral.ApplyDamage(new DamageRequest(
            9999,
            DamageType.Physical,
            "combat_hit_feedback_feral_death",
            CombatFaction.Player));
        _stage = 7;
        _stageElapsed = 0.0;
    }

    private void VerifyDeathFeedback()
    {
        var drop = GetTree().GetFirstNodeInGroup("item_drops_3d") as ItemDrop3D;
        if (_feral.IsAlive
            || _feral.IsPhysicsProcessing()
            || _feral.CollisionLayer != 0
            || _feral.CollisionMask != 0
            || _feralDeath.PlayCount != 1
            || drop == null)
        {
            if (_stageElapsed > 2.0)
            {
                Fail($"death ordering failed alive={_feral.IsAlive} physics={_feral.IsPhysicsProcessing()} collision={_feral.CollisionLayer}/{_feral.CollisionMask} plays={_feralDeath.PlayCount} drop={drop != null}");
            }

            return;
        }

        _stage = 8;
        _stageElapsed = 0.0;
    }

    private void StartDynamicFeralTest()
    {
        var feralScene = GD.Load<PackedScene>("res://scenes3d/Feral3D.tscn");
        if (feralScene == null)
        {
            Fail("could not load dynamic Feral scene");
            return;
        }

        _dynamicFeral = feralScene.Instantiate<FeralController3D>();
        _dynamicFeral.ForceGuaranteedDropForTest = true;
        _arena.AddChild(_dynamicFeral);
        _dynamicFeral.GlobalPosition = new Vector3(5.0f, 0.0f, 0.0f);
        _stage = 9;
        _stageElapsed = 0.0;
    }

    private void VerifyDynamicFeralFeedback()
    {
        if (_dynamicFeral == null || !GodotObject.IsInstanceValid(_dynamicFeral))
        {
            Fail("dynamic Feral did not enter the arena");
            return;
        }

        if (_stageElapsed < 0.1)
        {
            return;
        }

        _dynamicFeral.SetPhysicsProcess(false);
        var countBefore = _numbers.SpawnedCount;
        var result = _dynamicFeral.ApplyDamage(new DamageRequest(
            5,
            DamageType.Physical,
            "combat_hit_feedback_dynamic_feral",
            CombatFaction.Player));
        if (result.DamageApplied != 5
            || _numbers.SpawnedCount != countBefore + 1
            || _numbers.LastSpawnedText != "5")
        {
            if (_stageElapsed > 1.5)
            {
                Fail($"dynamic Feral did not publish damage feedback result={result.DamageApplied} count={_numbers.SpawnedCount}/{countBefore + 1} text={_numbers.LastSpawnedText}");
            }

            return;
        }

        _dynamicFeral.QueueFree();
        _stage = 10;
        _stageElapsed = 0.0;
    }

    private void VerifyBossMapComplete()
    {
        _boss.ApplyDamage(new DamageRequest(
            9999,
            DamageType.Physical,
            "combat_hit_feedback_boss_death",
            CombatFaction.Player));
        if (_flow.State != GameFlowState.MapComplete || !GetTree().Paused)
        {
            Fail($"Boss death did not immediately enter MapComplete state={_flow.State} paused={GetTree().Paused}");
            return;
        }

        GD.Print("COMBAT_HIT_FEEDBACK_3D_PASS damage_number=true zero_damage=true spread_numbers=2 pause_freeze=true hit_flash=true death_cleanup=true loot=true dynamic_source=true boss_map_complete=true");
        GetTree().Quit(0);
    }

    private void Fail(string reason)
    {
        GD.PushError($"COMBAT_HIT_FEEDBACK_3D_FAIL {reason}");
        GetTree().Quit(1);
    }
}
