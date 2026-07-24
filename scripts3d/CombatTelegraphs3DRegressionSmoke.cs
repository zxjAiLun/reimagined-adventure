using Godot;

/// <summary>
/// Deterministic contract smoke for the Stage 2B enemy attack timelines.
/// The smoke owns no combat rules; it only observes the public actor state.
/// </summary>
public partial class CombatTelegraphs3DRegressionSmoke : Node
{
    private TestArena3D _arena;
    private PlayerController3D _player;
    private FeralController3D _feral;
    private SpitterController3D _spitter;
    private BrimstoneColossusController3D _boss;
    private GameFlowController3D _flow;
    private int _stage;
    private double _stageElapsed;
    private double _totalElapsed;
    private int _feralWindupHealth;
    private int _feralWindupImpactCount;
    private int _spitterWindupProjectileCount;
    private int _spitterWindupShotCount;
    private Vector3 _spitterWindupDirection;
    private Vector3 _spitterTelegraphStart;
    private Vector3 _spitterTelegraphEnd;
    private int _pausedFeralImpactCount;
    private int _pausedFeralHealth;
    private float _pausedFeralProgress;
    private int _pausedSpitterShotCount;
    private float _pausedSpitterProgress;
    private int _slamWindupHealth;
    private int _slamWindupImpactCount;
    private Vector3 _slamTelegraphCenter;
    private float _slamTelegraphRadius;
    private int _spearLaunchCount;
    private Vector3 _spearTelegraphDirection;
    private Vector3 _spearTelegraphStart;
    private Vector3 _spearTelegraphEnd;
    private bool _movedSpitterTarget;
    private bool _movedSpearTarget;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _arena = GetNode<TestArena3D>("Arena3D");
        // Keep the observer alive while the gameplay arena follows the real
        // pause boundary used by GameFlowController3D.
        _arena.ProcessMode = ProcessModeEnum.Pausable;
        _player = _arena.GetNode<PlayerController3D>("Player3D");
        _feral = _arena.GetNode<FeralController3D>("Feral3D");
        _spitter = _arena.GetNode<SpitterController3D>("Spitter3D");
        _boss = _arena.GetNode<BrimstoneColossusController3D>("BrimstoneColossus3D");
        _flow = _arena.GetNode<GameFlowController3D>("GameFlow3D");

        _feral.SetPhysicsProcess(false);
        _spitter.SetPhysicsProcess(false);
        _boss.SetPhysicsProcess(false);
        _feral.GlobalPosition = _player.GlobalPosition + new Vector3(1.0f, 0.0f, 0.0f);
        _spitter.GlobalPosition = _player.GlobalPosition + new Vector3(0.0f, 0.0f, 5.0f);
        _boss.GlobalPosition = _player.GlobalPosition + new Vector3(3.0f, 0.0f, 0.0f);
        ClearEnemyProjectiles();
    }

    public override void _Process(double delta)
    {
        _totalElapsed += delta;
        _stageElapsed += delta;
        if (_totalElapsed > 30.0)
        {
            Fail($"timeout stage={_stage}");
            return;
        }

        switch (_stage)
        {
            case 0:
                StartFeralAttack();
                break;
            case 1:
                ObserveFeralWindup();
                break;
            case 2:
                ObserveFeralImpact();
                break;
            case 3:
                ObserveFeralRecovery();
                break;
            case 4:
                StartSpitterAttack();
                break;
            case 5:
                ObserveSpitterWindup();
                break;
            case 6:
                ObserveSpitterLaunch();
                break;
            case 7:
                StartPausedFeralAttack();
                break;
            case 8:
                ObservePausedFeralAttack();
                break;
            case 9:
                ResumeFeralAfterPause();
                break;
            case 10:
                StartPausedSpitterAttack();
                break;
            case 11:
                ObservePausedSpitterAttack();
                break;
            case 12:
                StartSlamAttack();
                break;
            case 13:
                ObserveSlamWindup();
                break;
            case 14:
                ObserveSlamImpact();
                break;
            case 15:
                StartSpearAttack();
                break;
            case 16:
                ObserveSpearLaunch();
                break;
        }
    }

    private void StartFeralAttack()
    {
        _flow.RestoreState(GameFlowState.Playing);
        _player.GlobalPosition = Vector3.Zero;
        _feral.GlobalPosition = new Vector3(1.0f, 0.0f, 0.0f);
        _feral.SetPhysicsProcess(true);
        NextStage();
    }

    private void ObserveFeralWindup()
    {
        if (_feral.State == FeralState3D.Windup)
        {
            if (_feral.ActiveTelegraph == null || !_feral.ActiveTelegraph.IsActive)
            {
                Fail("Feral entered Windup without an active area telegraph");
                return;
            }

            _feralWindupHealth = _player.CurrentHealth;
            _feralWindupImpactCount = _feral.ImpactCount;
            NextStage();
            return;
        }

        FailIfTimedOut("Feral did not enter Windup");
    }

    private void ObserveFeralImpact()
    {
        if (_feral.ImpactCount == _feralWindupImpactCount + 1)
        {
            if (_player.CurrentHealth >= _feralWindupHealth
                || _feral.ActiveTelegraph != null
                || _feral.SuccessfulContactAttackCount < 1)
            {
                Fail("Feral Impact did not apply exactly one contact hit");
                return;
            }

            NextStage();
            return;
        }

        if (_player.CurrentHealth != _feralWindupHealth
            || _feral.ImpactCount != _feralWindupImpactCount)
        {
            Fail("Feral caused damage or counted an impact during Windup");
            return;
        }

        if (_feral.State == FeralState3D.Windup && _feral.ActiveTelegraph == null)
        {
            Fail("Feral Windup lost its telegraph before Impact");
            return;
        }

        FailIfTimedOut("Feral did not reach Impact");
    }

    private void ObserveFeralRecovery()
    {
        if ((_feral.State == FeralState3D.Recovery
                || _feral.State == FeralState3D.Chasing)
            && _stageElapsed > _feral.AttackRecoverySeconds + 0.12f)
        {
            if (_feral.ImpactCount != _feralWindupImpactCount + 1
                || _player.CurrentHealth >= _feralWindupHealth)
            {
                Fail("Feral Recovery applied a second contact hit");
                return;
            }

            _feral.SetPhysicsProcess(false);
            _feral.GlobalPosition = new Vector3(20.0f, 0.0f, 20.0f);
            NextStage();
            return;
        }

        FailIfTimedOut("Feral did not remain in Recovery long enough");
    }

    private void StartSpitterAttack()
    {
        _spitter.GlobalPosition = new Vector3(0.0f, 0.0f, 5.0f);
        _spitter.SetPhysicsProcess(true);
        ClearEnemyProjectiles();
        NextStage();
    }

    private void ObserveSpitterWindup()
    {
        if (_spitter.State == SpitterState3D.Windup)
        {
            if (_spitter.ActiveTelegraph == null || !_spitter.ActiveTelegraph.IsActive)
            {
                Fail("Spitter entered Windup without a line telegraph");
                return;
            }

            _spitterWindupProjectileCount = GetEnemyProjectileCount();
            _spitterWindupShotCount = _spitter.ProjectileShotCount;
            _spitterWindupDirection = _spitter.ActiveTelegraph.LockedDirection;
            _spitterTelegraphStart = _spitter.ActiveTelegraph.StartPosition;
            _spitterTelegraphEnd = _spitter.ActiveTelegraph.EndPosition;
            if (_spitterTelegraphStart.DistanceTo(
                    _spitterTelegraphEnd
                    - _spitterWindupDirection * _spitter.ActiveTelegraph.Length) > 0.001f
                || !IsPointOnLineSegmentXZ(
                    _spitter.LockedTargetPosition,
                    _spitterTelegraphStart,
                    _spitterTelegraphEnd))
            {
                Fail("Spitter telegraph geometry does not cover the locked target");
                return;
            }

            NextStage();
            return;
        }

        FailIfTimedOut("Spitter did not enter Windup");
    }

    private void ObserveSpitterLaunch()
    {
        if (!_movedSpitterTarget)
        {
            _player.GlobalPosition += new Vector3(2.0f, 0.0f, -1.5f);
            _movedSpitterTarget = true;
        }

        if (_spitter.State == SpitterState3D.Windup)
        {
            if (_spitter.ActiveTelegraph == null
                || !_spitter.ActiveTelegraph.IsActive
                || _spitter.ActiveTelegraph.StartPosition.DistanceTo(_spitterTelegraphStart) > 0.001f
                || _spitter.ActiveTelegraph.EndPosition.DistanceTo(_spitterTelegraphEnd) > 0.001f
                || GetEnemyProjectileCount() != _spitterWindupProjectileCount
                || _spitter.ProjectileShotCount != _spitterWindupShotCount)
            {
                Fail("Spitter Windup lost its telegraph geometry or generated a projectile");
                return;
            }

            return;
        }

        if (_spitter.ProjectileShotCount == _spitterWindupShotCount + 1)
        {
            if (_spitter.LastLaunchDirection.DistanceTo(_spitterWindupDirection) > 0.001f
                || _spitter.ActiveTelegraph != null)
            {
                Fail("Spitter Launch did not use the locked telegraph direction");
                return;
            }

            _spitter.SetPhysicsProcess(false);
            NextStage();
            return;
        }

        FailIfTimedOut("Spitter did not reach Launch");
    }

    private void StartPausedFeralAttack()
    {
        _flow.RestoreState(GameFlowState.Playing);
        _player.GlobalPosition = Vector3.Zero;
        _feral.GlobalPosition = new Vector3(1.0f, 0.0f, 0.0f);
        _feral.AttackCooldown = 0.1f;
        _feral.SetPhysicsProcess(true);
        NextStage();
    }

    private void ObservePausedFeralAttack()
    {
        if (_feral.State == FeralState3D.Windup)
        {
            _pausedFeralImpactCount = _feral.ImpactCount;
            _pausedFeralHealth = _player.CurrentHealth;
            _pausedFeralProgress = _feral.ActiveTelegraph?.Progress ?? -1.0f;
            _flow.RestoreState(GameFlowState.GameOver);
            _stageElapsed = 0.0;
            NextStage();
            return;
        }

        FailIfTimedOut("Feral did not enter the paused Windup test");
    }

    private void ResumeFeralAfterPause()
    {
        if (_stageElapsed < 0.8)
        {
            return;
        }

        if (_feral.ImpactCount != _pausedFeralImpactCount
            || _player.CurrentHealth != _pausedFeralHealth
            || _feral.ActiveTelegraph == null
            || Mathf.Abs(_feral.ActiveTelegraph.Progress - _pausedFeralProgress) > 0.001f)
        {
            Fail($"GameOver pause advanced Feral telegraph or Impact flow={_flow.State} paused={GetTree().Paused} impact={_feral.ImpactCount}/{_pausedFeralImpactCount} hp={_player.CurrentHealth}/{_pausedFeralHealth} active={_feral.ActiveTelegraph != null} progress={_feral.ActiveTelegraph?.Progress ?? -1.0f}/{_pausedFeralProgress}");
            return;
        }

        _feral.SetPhysicsProcess(false);
        _flow.RestoreState(GameFlowState.Playing);
        NextStage();
    }

    private void StartPausedSpitterAttack()
    {
        _feral.SetPhysicsProcess(false);
        _spitter.GlobalPosition = new Vector3(0.0f, 0.0f, 5.0f);
        _spitter.AttackCooldown = 0.1f;
        _spitter.SetPhysicsProcess(true);
        ClearEnemyProjectiles();
        NextStage();
    }

    private void ObservePausedSpitterAttack()
    {
        if (_flow.State == GameFlowState.MapComplete)
        {
            if (_stageElapsed < 0.8)
            {
                return;
            }

            if (_spitter.ProjectileShotCount != _pausedSpitterShotCount
                || _spitter.ActiveTelegraph == null
                || Mathf.Abs(_spitter.ActiveTelegraph.Progress - _pausedSpitterProgress) > 0.001f)
            {
                Fail("MapComplete pause advanced Spitter telegraph or Launch");
                return;
            }

            _spitter.SetPhysicsProcess(false);
            _flow.RestoreState(GameFlowState.Playing);
            NextStage();
            return;
        }

        if (_spitter.State == SpitterState3D.Windup)
        {
            _pausedSpitterShotCount = _spitter.ProjectileShotCount;
            _pausedSpitterProgress = _spitter.ActiveTelegraph?.Progress ?? -1.0f;
            _flow.RestoreState(GameFlowState.MapComplete);
            _stageElapsed = 0.0;
            return;
        }

        FailIfTimedOut("Spitter did not enter the paused Windup test");
    }

    private void StartSlamAttack()
    {
        _flow.RestoreState(GameFlowState.Playing);
        _spitter.SetPhysicsProcess(false);
        _player.GlobalPosition = Vector3.Zero;
        _boss.GlobalPosition = new Vector3(2.0f, 0.0f, 0.0f);
        _boss.SetPhysicsProcess(true);
        ClearEnemyProjectiles();
        NextStage();
    }

    private void ObserveSlamWindup()
    {
        if (_boss.State == BrimstoneColossusState3D.PreparingSlam)
        {
            if (_boss.ActiveSlamTelegraph == null || !_boss.ActiveSlamTelegraph.IsActive)
            {
                Fail("Slam Windup has no active area telegraph");
                return;
            }

            _slamWindupHealth = _player.CurrentHealth;
            _slamWindupImpactCount = _boss.MagmaSlamImpactCount;
            _slamTelegraphCenter = _boss.ActiveSlamTelegraph.TelegraphWorldPosition;
            _slamTelegraphRadius = _boss.ActiveSlamTelegraph.Radius;
            NextStage();
            return;
        }

        FailIfTimedOut("Boss did not enter Magma Slam Windup");
    }

    private void ObserveSlamImpact()
    {
        if (_boss.MagmaSlamImpactCount == _slamWindupImpactCount + 1)
        {
            if (_player.CurrentHealth >= _slamWindupHealth
                || _boss.ActiveSlamTelegraph != null
                || _boss.LastSlamImpactCenter.DistanceTo(_slamTelegraphCenter) > 0.001f
                || Mathf.Abs(_boss.LastSlamImpactRadius - _slamTelegraphRadius) > 0.001f
                || Mathf.Abs(_boss.LastSlamImpactRadius - _boss.SlamRadius) > 0.001f)
            {
                Fail($"Magma Slam impact did not match its telegraph area center={_boss.LastSlamImpactCenter} telegraph={_slamTelegraphCenter} radius={_boss.LastSlamImpactRadius}/{_slamTelegraphRadius}/{_boss.SlamRadius}");
                return;
            }

            NextStage();
            return;
        }

        if (_boss.State == BrimstoneColossusState3D.PreparingSlam)
        {
            if (_player.CurrentHealth != _slamWindupHealth
                || _boss.ActiveSlamTelegraph == null
                || !_boss.ActiveSlamTelegraph.IsActive)
            {
                Fail("Magma Slam damaged the player during Windup");
            }

            return;
        }

        FailIfTimedOut("Boss did not reach Magma Slam Impact");
    }

    private void StartSpearAttack()
    {
        if (_boss.State == BrimstoneColossusState3D.Recovering)
        {
            return;
        }

        if (_boss.State == BrimstoneColossusState3D.Idle
            || _boss.State == BrimstoneColossusState3D.Chasing)
        {
            NextStage();
        }
    }

    private void ObserveSpearLaunch()
    {
        if (_boss.State == BrimstoneColossusState3D.PreparingSpear)
        {
            if (_boss.ActiveSpearTelegraph == null || !_boss.ActiveSpearTelegraph.IsActive)
            {
                Fail("Spear Windup has no active line telegraph");
                return;
            }

            _spearLaunchCount = _boss.FlameSpearLaunchCount;
            _spearTelegraphDirection = _boss.ActiveSpearTelegraph.LockedDirection;
            _spearTelegraphStart = _boss.ActiveSpearTelegraph.StartPosition;
            _spearTelegraphEnd = _boss.ActiveSpearTelegraph.EndPosition;
            if (!_movedSpearTarget)
            {
                _player.GlobalPosition = _boss.GlobalPosition + new Vector3(-3.0f, 0.0f, 2.0f);
                _movedSpearTarget = true;
            }

            return;
        }

        if (_boss.FlameSpearLaunchCount == _spearLaunchCount + 1)
        {
            if (_boss.LastSpearLaunchDirection.DistanceTo(_spearTelegraphDirection) > 0.001f
                || _boss.ActiveSpearTelegraph != null)
            {
                Fail("Flame Spear did not use the locked telegraph direction");
                return;
            }

            GD.Print(
                "COMBAT_TELEGRAPHS_3D_PASS feral=true spitter=true slam=true spear=true pause=true");
            GetTree().Quit(0);
            return;
        }

        FailIfTimedOut("Boss did not reach Flame Spear Launch");
    }

    private static bool IsPointOnLineSegmentXZ(Vector3 point, Vector3 start, Vector3 end)
    {
        var point2 = new Vector2(point.X, point.Z);
        var start2 = new Vector2(start.X, start.Z);
        var end2 = new Vector2(end.X, end.Z);
        var segment = end2 - start2;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return point2.DistanceTo(start2) <= 0.05f;
        }

        var t = Mathf.Clamp((point2 - start2).Dot(segment) / lengthSquared, 0.0f, 1.0f);
        var closest = start2 + segment * t;
        return point2.DistanceTo(closest) <= 0.05f;
    }

    private void NextStage()
    {
        _stage++;
        _stageElapsed = 0.0;
    }

    private void FailIfTimedOut(string reason)
    {
        if (_stageElapsed > 5.0)
        {
            Fail(reason);
        }
    }

    private void Fail(string reason)
    {
        GD.PushError($"COMBAT_TELEGRAPHS_3D_FAIL {reason}");
        GetTree().Quit(1);
    }

    private int GetEnemyProjectileCount()
    {
        return GetTree().GetNodesInGroup("enemy_projectiles_3d").Count;
    }

    private void ClearEnemyProjectiles()
    {
        foreach (var node in GetTree().GetNodesInGroup("enemy_projectiles_3d"))
        {
            if (node is Node projectile)
            {
                projectile.QueueFree();
            }
        }
    }
}
