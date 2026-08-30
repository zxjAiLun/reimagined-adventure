using System;
using System.Linq;
using Arpg.Domain;
using Godot;

public partial class CombatImpactFeedback3DRegressionSmoke : Node
{
    private enum Stage
    {
        Bind,
        VerifyLightImpact,
        WaitLightRecovery,
        VerifyHeavyImpact,
        SpawnDynamic,
        VerifyDynamic,
        VerifyPauseFreeze,
        VerifyExitCleanup,
    }

    private Stage _stage;
    private double _elapsed;
    private bool _complete;
    private RunSessionNode _run;
    private TestArena3D _arena;
    private PlayerController3D _player;
    private FeralController3D _feral;
    private FeralController3D _dynamic;
    private CombatImpactFeedback3D _feedback;
    private IsometricCameraRig3D _camera;
    private int _baselineSources;
    private ulong _pauseStarted;
    private Vector3 _pausedCameraPosition;
    private Vector3 _pausedShakeOffset;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _run = GetNodeOrNull<RunSessionNode>("RunShell3D");
        if (_run == null)
        {
            Fail("impact feedback smoke is missing RunShell3D");
        }
    }

    public override void _Process(double delta)
    {
        if (_complete)
        {
            return;
        }
        _elapsed += delta;
        if (_elapsed > 20.0)
        {
            Fail($"impact feedback smoke timed out at {_stage}");
            return;
        }

        switch (_stage)
        {
            case Stage.Bind: Bind(); break;
            case Stage.VerifyLightImpact: VerifyLightImpact(); break;
            case Stage.WaitLightRecovery: WaitLightRecovery(); break;
            case Stage.VerifyHeavyImpact: VerifyHeavyImpact(); break;
            case Stage.SpawnDynamic: SpawnDynamic(); break;
            case Stage.VerifyDynamic: VerifyDynamic(); break;
            case Stage.VerifyPauseFreeze: VerifyPauseFreeze(); break;
            case Stage.VerifyExitCleanup: VerifyExitCleanup(); break;
        }
    }

    private void Bind()
    {
        _arena ??= _run.CurrentMap3D;
        _player ??= _arena?.GetNodeOrNull<PlayerController3D>("Player3D");
        _feedback ??= _arena?.GetNodeOrNull<CombatImpactFeedback3D>("CombatImpactFeedback3D");
        _camera ??= _arena?.GetNodeOrNull<IsometricCameraRig3D>("CameraRig");
        _feral ??= _arena?.GetNodeOrNull<Node3D>("EnemyContainer")?.GetChildren()
            .OfType<FeralController3D>().FirstOrDefault(enemy => enemy.IsAlive);
        if (_player == null || _feedback == null || _camera == null || _feral == null)
        {
            return;
        }

        _baselineSources = _feedback.RegisteredSourceCount;
        var zero = _player.ApplyDamage(new DamageRequest(0, DamageType.Physical, "impact_zero", CombatFaction.Enemy));
        if (zero.DamageApplied != 0 || _feedback.ImpactCount != 0)
        {
            Fail("zero damage produced impact presentation");
            return;
        }

        var result = _player.ApplyDamage(new DamageRequest(5, DamageType.Physical, "impact_light", CombatFaction.Enemy));
        if (result.DamageApplied <= 0)
        {
            Fail("light impact fixture caused no authoritative damage");
            return;
        }
        Advance(Stage.VerifyLightImpact);
    }

    private void VerifyLightImpact()
    {
        var audio = _feedback.GetNodeOrNull<AudioStreamPlayer>("ImpactAudio");
        if (_feedback.ImpactCount != 1
            || _feedback.AudioTriggerCount != 1
            || _feedback.ShakeTriggerCount != 1
            || _feedback.HitStopTriggerCount != 1
            || !_feedback.IsHitStopActive
            || _camera.ShakeRequestCount != 1
            || Math.Abs(Engine.TimeScale - _feedback.HitStopTimeScale) > 0.001
            || audio?.Stream == null)
        {
            Fail("light impact did not trigger audio, shake, and hit-stop exactly once");
            return;
        }
        Advance(Stage.WaitLightRecovery);
    }

    private void WaitLightRecovery()
    {
        if (_feedback.IsHitStopActive || _camera.IsShaking)
        {
            return;
        }
        if (Math.Abs(Engine.TimeScale - 1.0) > 0.001)
        {
            Fail("hit-stop did not restore the baseline time scale");
            return;
        }

        var result = _feral.ApplyDamage(new DamageRequest(20, DamageType.Physical, "impact_heavy", CombatFaction.Player));
        if (result.DamageApplied < _feedback.HeavyDamageThreshold)
        {
            Fail("heavy impact fixture did not cross the configured threshold");
            return;
        }
        Advance(Stage.VerifyHeavyImpact);
    }

    private void VerifyHeavyImpact()
    {
        if (_feedback.ImpactCount != 2
            || _feedback.AudioTriggerCount != 2
            || _feedback.ShakeTriggerCount != 2
            || _feedback.HitStopTriggerCount != 2
            || _camera.ShakeRequestCount != 2)
        {
            Fail("heavy impact did not use the shared feedback path");
            return;
        }
        Advance(Stage.SpawnDynamic);
    }

    private void SpawnDynamic()
    {
        if (_feedback.IsHitStopActive)
        {
            return;
        }
        var scene = GD.Load<PackedScene>("res://scenes3d/Feral3D.tscn");
        _dynamic = scene?.Instantiate<FeralController3D>();
        if (_dynamic == null)
        {
            Fail("could not instantiate dynamic impact source");
            return;
        }
        _dynamic.Name = "ImpactDynamicFeral3D";
        _dynamic.Position = new Vector3(-2.0f, 0.0f, 2.0f);
        _arena.GetNode<Node3D>("EnemyContainer").AddChild(_dynamic);
        Advance(Stage.VerifyDynamic);
    }

    private void VerifyDynamic()
    {
        if (_feedback.RegisteredSourceCount <= _baselineSources)
        {
            return;
        }
        var before = _feedback.ImpactCount;
        var result = _dynamic.ApplyDamage(new DamageRequest(5, DamageType.Physical, "impact_dynamic", CombatFaction.Player));
        if (result.DamageApplied <= 0 || _feedback.ImpactCount != before + 1)
        {
            Fail("dynamic damage source did not register with map impact feedback");
            return;
        }

        _pausedCameraPosition = _camera.GlobalPosition;
        _pausedShakeOffset = _camera.ShakeOffset;
        GetTree().Paused = true;
        _pauseStarted = Time.GetTicksMsec();
        Advance(Stage.VerifyPauseFreeze);
    }

    private void VerifyPauseFreeze()
    {
        if (Time.GetTicksMsec() - _pauseStarted < 100)
        {
            return;
        }
        if (!GetTree().Paused
            || _camera.GlobalPosition.DistanceTo(_pausedCameraPosition) > 0.001f
            || _camera.ShakeOffset.DistanceTo(_pausedShakeOffset) > 0.001f
            || _feedback.IsHitStopActive
            || Math.Abs(Engine.TimeScale - 1.0) > 0.001)
        {
            Fail("paused camera advanced or hit-stop failed to release on real time");
            return;
        }

        GetTree().Paused = false;
        _player.ApplyDamage(new DamageRequest(5, DamageType.Physical, "impact_exit", CombatFaction.Enemy));
        if (!_feedback.IsHitStopActive)
        {
            Fail("exit cleanup fixture did not start hit-stop");
            return;
        }
        _run.QueueFree();
        _run = null;
        Advance(Stage.VerifyExitCleanup);
    }

    private void VerifyExitCleanup()
    {
        if (GetTree().GetNodesInGroup("run_sessions").Count > 0)
        {
            return;
        }
        if (Math.Abs(Engine.TimeScale - 1.0) > 0.001)
        {
            Fail("map exit left Engine.TimeScale modified");
            return;
        }

        _complete = true;
        GD.Print("COMBAT_IMPACT_FEEDBACK_3D_REGRESSION_PASS zero_filtered=true light=true heavy=true audio=true shake=true hit_stop=true dynamic=true pause=true exit_cleanup=true");
        GetTree().Quit();
    }

    private void Advance(Stage stage)
    {
        _stage = stage;
        _elapsed = 0.0;
    }

    private void Fail(string reason)
    {
        if (_complete) return;
        _complete = true;
        GetTree().Paused = false;
        Engine.TimeScale = 1.0;
        GD.PushError($"COMBAT_IMPACT_FEEDBACK_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}
