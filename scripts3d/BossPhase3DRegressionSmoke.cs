using System;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Runtime contract for the independent Brimstone phase driver. The smoke
/// observes map-local metadata and real telegraph/projectile nodes instead of
/// exposing another Godot-heavy diagnostic API on the boss controller.
/// </summary>
public partial class BossPhase3DRegressionSmoke : Node
{
    private double _elapsed;
    private int _stage;
    private bool _complete;
    private TestArena3D _arena;
    private BrimstoneColossusController3D _boss;
    private PlayerController3D _player;
    private GameFlowController3D _flow;
    private PlayerBuildController3D _build;
    private HealthComponent _playerHealth;
    private int _ringHealthBefore;
    private int _phaseTwoAdds;
    private int _lavaCountBeforePause;
    private Vector3 _barrageDirection0;
    private int _barrageProjectileCountBefore;
    private bool _bossEventsBound;
    private bool _ringTelegraphObserved;
    private bool _barrageTelegraphObserved;
    private bool _barrageTargetMoved;
    private int _observedBarrageCount;
    private int _barrageLaunchCountBefore;
    private bool _ringPauseRequested;
    private bool _ringPauseVerified;
    private float _ringProgressBeforePause;
    private int _ringImpactBeforePause;
    private int _ringHealthBeforePause;
    private double _ringNormalStartedAt;
    private double _ringNormalDuration;
    private double _ringChilledStartedAt;
    private double _ringChilledDuration;
    private bool _ringNormalTimingVerified;
    private bool _ringChilledTimingVerified;
    private bool _ringChilledApplied;
    private bool _barragePauseRequested;
    private bool _barragePauseVerified;
    private float _barrageProgressBeforePause;
    private int _barrageLaunchBeforePause;
    private int _barrageHealthBeforePause;
    private double _barrageNormalStartedAt;
    private double _barrageNormalDuration;
    private double _barrageChilledStartedAt;
    private double _barrageChilledDuration;
    private int _barrageNormalLaunchBefore;
    private int _barrageChilledLaunchBefore;
    private bool _barrageNormalTimingVerified;
    private bool _barrageChilledTimingVerified;
    private bool _barrageChilledApplied;
    private bool _lavaPauseRequested;
    private bool _lavaPauseVerified;
    private float _lavaProgressBeforePause;

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

        try
        {
            BindRuntime();
            if (_boss == null || _player == null || _flow == null)
            {
                if (_elapsed > 12.0)
                {
                    Fail("boss phase smoke runtime did not become ready");
                }

                return;
            }

            switch (_stage)
            {
                case 0:
                    PrepareBoss();
                    break;
                case 1:
                    VerifyPhaseTwoAndRing();
                    break;
                case 2:
                    EnterPhaseThree();
                    break;
                case 3:
                    VerifyEmberBarrage();
                    break;
                case 4:
                    VerifyLavaPauseAndDeath();
                    break;
            }

            if (_elapsed > 36.0 && !_complete)
            {
                Fail($"boss phase smoke timed out at stage {_stage}");
            }
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    public override void _ExitTree()
    {
        if (_bossEventsBound && GodotObject.IsInstanceValid(_boss))
        {
            _boss.BossAttackStarted -= OnBossAttackStarted;
        }
    }

    private void BindRuntime()
    {
        _arena ??= GetNodeOrNull<TestArena3D>("Arena3D");
        if (_arena == null)
        {
            return;
        }

        _player ??= _arena.GetNodeOrNull<PlayerController3D>("Player3D");
        _flow ??= _arena.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        _build ??= _arena.GetNodeOrNull<BuildIntermissionController3D>("BuildIntermission3D")
            ?.GetParent()?.GetNodeOrNull<PlayerBuildController3D>("Player3D/PlayerBuildController3D");
        _build ??= _player?.GetNodeOrNull<PlayerBuildController3D>("PlayerBuildController3D");
        _playerHealth ??= _player?.GetNodeOrNull<HealthComponent>("HealthComponent");

        _boss ??= _arena.GetNodeOrNull<BrimstoneColossusController3D>("BrimstoneColossus3D");
        if (_boss == null)
        {
            var scene = GD.Load<PackedScene>("res://scenes3d/BrimstoneColossus3D.tscn");
            if (scene != null)
            {
                _boss = scene.Instantiate<BrimstoneColossusController3D>();
                _boss.Name = "BrimstoneColossus3D";
                _arena.AddChild(_boss);
                _boss.GlobalPosition = Vector3.Zero;
            }
        }

        if (!_bossEventsBound && _boss != null)
        {
            _boss.BossAttackStarted += OnBossAttackStarted;
            _bossEventsBound = true;
        }

        var director = _arena.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        if (director != null)
        {
            director.Enabled = false;
            director.ProcessMode = ProcessModeEnum.Disabled;
        }
    }

    private void PrepareBoss()
    {
        if (_boss == null || !_boss.IsAlive || !HasMeta(_boss, "boss_phase_id"))
        {
            return;
        }

        _playerHealth?.SetMaxHealth(10000);
        _player.GlobalPosition = _boss.GlobalPosition + new Vector3(3.0f, 0.0f, 0.0f);
        if (MetaString(_boss, "boss_phase_id") != "phase-1")
        {
            Fail($"initial boss phase was not phase-1: {MetaString(_boss, "boss_phase_id")}");
            return;
        }

        _boss.ApplyDamage(new DamageRequest(
            60,
            DamageType.Physical,
            "boss_phase_smoke_phase_two",
            CombatFaction.Player));
        _stage = 1;
        _elapsed = 0.0;
    }

    private void VerifyPhaseTwoAndRing()
    {
        if (MetaString(_boss, "boss_phase_id") != "phase-2")
        {
            return;
        }

        _phaseTwoAdds = MetaInt(_boss, "boss_add_count");
        if (_phaseTwoAdds < 2)
        {
            Fail($"phase two did not spawn two map-local adds: {_phaseTwoAdds}");
            return;
        }

        if (MetaInt(_boss, "boss_molten_ring_count") < 1
            && MetaString(_boss, "boss_current_attack_id") != "molten_ring")
        {
            return;
        }

        if (MetaInt(_boss, "boss_molten_ring_impact_count") == 0
            && !_ringTelegraphObserved
            && !FindActiveRing())
        {
            Fail("Molten Ring did not create a visible telegraph");
            return;
        }

        if (!_ringTelegraphObserved && FindActiveRing())
        {
            _ringTelegraphObserved = true;
            _ringHealthBefore = _player.CurrentHealth;
        }

        if (!_ringNormalTimingVerified)
        {
            if (_ringNormalStartedAt <= 0.0
                || MetaInt(_boss, "boss_molten_ring_impact_count") < 1
                || FindActiveRing())
            {
                return;
            }

            _ringNormalDuration = NowSeconds() - _ringNormalStartedAt;
            AssertChilledTimingBaseline(
                "Molten Ring normal",
                _ringNormalDuration);
            _ringNormalTimingVerified = true;
            return;
        }

        if (!_ringChilledTimingVerified)
        {
            if (!_ringChilledApplied
                || _ringChilledStartedAt <= 0.0
                || MetaInt(_boss, "boss_molten_ring_impact_count") < 2
                || FindActiveRing())
            {
                return;
            }

            _ringChilledDuration = NowSeconds() - _ringChilledStartedAt;
            AssertChilledActionTiming(
                "Molten Ring",
                _ringNormalDuration,
                _ringChilledDuration);
            _ringChilledTimingVerified = true;
            return;
        }

        if (MetaInt(_boss, "boss_molten_ring_count") < 3)
        {
            return;
        }

        if (!_ringPauseRequested)
        {
            var activeRing = GetActiveRing();
            if (activeRing != null)
            {
                _ringPauseRequested = true;
                _ringProgressBeforePause = activeRing.Progress;
                _ringImpactBeforePause = MetaInt(_boss, "boss_molten_ring_impact_count");
                _ringHealthBeforePause = _player.CurrentHealth;
                SendKey(Key.I);
            }
        }

        if (_ringPauseRequested && !_ringPauseVerified)
        {
            if (_build?.IsOpen != true || !GetTree().Paused)
            {
                return;
            }

            if (_elapsed < 0.95)
            {
                return;
            }

            var pausedRing = GetActiveRing();
            if (pausedRing == null
                || Math.Abs(pausedRing.Progress - _ringProgressBeforePause) > 0.001f
                || MetaInt(_boss, "boss_molten_ring_impact_count") != _ringImpactBeforePause
                || _player.CurrentHealth != _ringHealthBeforePause)
            {
                Fail("inventory pause advanced Molten Ring");
                return;
            }

            SendKey(Key.I);
            _ringPauseVerified = true;
            return;
        }

        if (_ringPauseVerified && _build?.IsOpen == true)
        {
            return;
        }

        if (MetaInt(_boss, "boss_molten_ring_impact_count") < 3)
        {
            return;
        }

        var ringPassed = MetaInt(_boss, "boss_molten_ring_impact_count") >= 3
            && _player.CurrentHealth < _ringHealthBeforePause
            && !FindActiveRing();
        if (!ringPassed || !_ringTelegraphObserved)
        {
            var activeRing = GetActiveRing();
            Fail($"Molten Ring impact contract failed hp={_player.CurrentHealth}/{_ringHealthBefore} active={activeRing != null} player={_player.GlobalPosition} boss={_boss.GlobalPosition} ring={(activeRing == null ? "none" : $"{activeRing.StartPosition} inner={activeRing.InnerRadius} outer={activeRing.OuterRadius} isActive={activeRing.IsActive}")}");
            return;
        }

        _boss.ApplyDamage(new DamageRequest(
            70,
            DamageType.Physical,
            "boss_phase_smoke_phase_three",
            CombatFaction.Player));
        _stage = 2;
        _elapsed = 0.0;
    }

    private void EnterPhaseThree()
    {
        if (MetaString(_boss, "boss_phase_id") != "phase-3")
        {
            return;
        }

        if (MetaInt(_boss, "boss_phase_transition_count") != 2)
        {
            Fail("phase three transition count was not exactly two");
            return;
        }

        if (_boss.Ailments?.Collection.Has(AilmentKind.Chilled) == true)
        {
            Fail("Phase 2 Chilled did not expire before the Phase 3 baseline attack");
            return;
        }

        _boss.Ailments?.ResetPresentation();
        _player.GlobalPosition = _boss.GlobalPosition + new Vector3(3.0f, 0.0f, 0.0f);
        _stage = 3;
        _elapsed = 0.0;
    }

    private void VerifyEmberBarrage()
    {
        if (_observedBarrageCount == 0)
        {
            return;
        }

        if (!_barrageTelegraphObserved)
        {
            Fail("Ember Barrage did not create three telegraphs");
            return;
        }

        if (!_barrageNormalTimingVerified)
        {
            if (_barrageNormalStartedAt <= 0.0
                || MetaInt(_boss, "boss_ember_barrage_launch_count") <= _barrageNormalLaunchBefore
                || MetaInt(_boss, "boss_active_barrage_telegraph_count") != 0)
            {
                return;
            }

            _barrageNormalDuration = NowSeconds() - _barrageNormalStartedAt;
            AssertChilledTimingBaseline(
                "Ember Barrage normal",
                _barrageNormalDuration);
            _barrageNormalTimingVerified = true;
            return;
        }

        if (!_barrageChilledTimingVerified)
        {
            if (!_barrageChilledApplied
                || _barrageChilledStartedAt <= 0.0
                || MetaInt(_boss, "boss_ember_barrage_launch_count") <= _barrageChilledLaunchBefore
                || MetaInt(_boss, "boss_active_barrage_telegraph_count") != 0)
            {
                return;
            }

            _barrageChilledDuration = NowSeconds() - _barrageChilledStartedAt;
            AssertChilledActionTiming(
                "Ember Barrage",
                _barrageNormalDuration,
                _barrageChilledDuration);
            _barrageChilledTimingVerified = true;
            return;
        }

        if (!_barragePauseRequested)
        {
            var activeTelegraph = FindNodeRecursive<LineTelegraph3D>(this);
            if (activeTelegraph != null && activeTelegraph.IsActive)
            {
                _barragePauseRequested = true;
                _barrageProgressBeforePause = activeTelegraph.Progress;
                _barrageLaunchBeforePause = MetaInt(_boss, "boss_ember_barrage_launch_count");
                _barrageHealthBeforePause = _player.CurrentHealth;
                SendKey(Key.I);
            }
        }

        if (_barragePauseRequested && !_barragePauseVerified)
        {
            if (_build?.IsOpen != true || !GetTree().Paused)
            {
                return;
            }

            if (_elapsed < 0.95)
            {
                return;
            }

            var pausedTelegraph = FindNodeRecursive<LineTelegraph3D>(this);
            if (pausedTelegraph == null
                || !pausedTelegraph.IsActive
                || Math.Abs(pausedTelegraph.Progress - _barrageProgressBeforePause) > 0.001f
                || MetaInt(_boss, "boss_ember_barrage_launch_count") != _barrageLaunchBeforePause
                || _player.CurrentHealth != _barrageHealthBeforePause)
            {
                Fail("inventory pause advanced Ember Barrage");
                return;
            }

            SendKey(Key.I);
            _barragePauseVerified = true;
            return;
        }

        if (_barragePauseVerified && _build?.IsOpen == true)
        {
            return;
        }

        if (!_barrageTargetMoved)
        {
            _player.GlobalPosition = _boss.GlobalPosition + new Vector3(-4.0f, 0.0f, 2.0f);
            _barrageTargetMoved = true;
        }

        if (MetaInt(_boss, "boss_ember_barrage_launch_count") <= _barrageLaunchCountBefore)
        {
            return;
        }

        var launchDirection = MetaVector3(_boss, "boss_last_barrage_direction_0");
        var locked = launchDirection.DistanceTo(_barrageDirection0) < 0.001f;
        var launched = EnemyProjectileCount() >= _barrageProjectileCountBefore + 3;
        if (!locked || !launched || ActiveLineTelegraphCount() != 0)
        {
            Fail($"Ember Barrage lock/launch failed locked={locked} launched={launched} active={ActiveLineTelegraphCount()}");
            return;
        }

        _stage = 4;
        _elapsed = 0.0;
    }

    private void VerifyLavaPauseAndDeath()
    {
        if (MetaInt(_boss, "boss_lava_eruption_count") < 1)
        {
            return;
        }

        _lavaCountBeforePause = MetaInt(_boss, "boss_lava_eruption_count");
        if (MetaLong(_boss, "boss_last_hazard_seed") == 0)
        {
            Fail("lava eruption did not publish a deterministic non-zero seed");
            return;
        }

        var lava = FindNodeRecursive<BossLavaEruption3D>(this);
        var lavaTelegraph = lava?.GetChildren().OfType<CombatTelegraph3D>().FirstOrDefault();
        if (lava != null && lavaTelegraph != null && !_lavaPauseRequested)
        {
            _lavaPauseRequested = true;
            _lavaPauseVerified = false;
            _lavaProgressBeforePause = lavaTelegraph.Progress;
            _lavaCountBeforePause = MetaInt(_boss, "boss_lava_eruption_count");
            SendKey(Key.I);
        }

        if (_lavaPauseRequested && !_lavaPauseVerified)
        {
            if (_build?.IsOpen != true || !GetTree().Paused)
            {
                return;
            }

            if (_elapsed < 0.95)
            {
                return;
            }

            var pausedLava = FindNodeRecursive<BossLavaEruption3D>(this);
            var pausedLavaTelegraph = pausedLava?.GetChildren().OfType<CombatTelegraph3D>().FirstOrDefault();
            if (pausedLavaTelegraph == null
                || Math.Abs(pausedLavaTelegraph.Progress - _lavaProgressBeforePause) > 0.001f
                || MetaInt(_boss, "boss_lava_eruption_count") != _lavaCountBeforePause)
            {
                Fail("inventory pause advanced Lava Eruption");
                return;
            }

            SendKey(Key.I);
            _lavaPauseVerified = true;
            return;
        }

        if (_lavaPauseVerified && _build?.IsOpen == true)
        {
            return;
        }

        if (!_flow.RestoreState(GameFlowState.GameOver))
        {
            Fail("could not enter GameOver during lava windup");
            return;
        }

        if (_elapsed < 0.9)
        {
            return;
        }

        if (MetaInt(_boss, "boss_lava_eruption_count") != _lavaCountBeforePause
            || FindActiveRing()
            || ActiveLineTelegraphCount() != 0)
        {
            Fail("GameOver did not freeze and clean boss phase hazards");
            return;
        }

        _flow.RestoreState(GameFlowState.Playing);
        _boss.ApplyDamage(new DamageRequest(
            999999,
            DamageType.Physical,
            "boss_phase_smoke_lethal",
            CombatFaction.Player));
        if (_boss.IsAlive)
        {
            return;
        }

        if (_flow.State != GameFlowState.MapComplete
            || _boss.CollisionLayer != 0
            || _boss.CollisionMask != 0)
        {
            Fail($"boss death boundary failed flow={_flow.State} layer={_boss.CollisionLayer} mask={_boss.CollisionMask}");
            return;
        }

        _complete = true;
        var ringRatio = _ringChilledDuration / _ringNormalDuration;
        var barrageRatio = _barrageChilledDuration / _barrageNormalDuration;
        GD.Print($"BOSS_PHASE_3D_REGRESSION_PASS phases=true adds=true ring=true ring_action_speed=true ring_ratio={ringRatio:0.00} ring_geometry=true barrage=true barrage_action_speed=true barrage_ratio={barrageRatio:0.00} barrage_lock=true lava=true deterministic_seed=true inventory_pause_ring=true inventory_pause_barrage=true inventory_pause_lava=true pause_cancel=true death_cleanup=true map_complete=true");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GetTree().Quit();
    }

    private bool FindActiveRing()
    {
        return GetActiveRing() != null;
    }

    private RingTelegraph3D GetActiveRing()
    {
        var ring = FindNodeRecursive<RingTelegraph3D>(this);
        if (ring != null && ring.IsActive)
        {
            return ring;
        }

        return null;
    }

    private int ActiveLineTelegraphCount()
    {
        return CountActiveLineTelegraphs(this);
    }

    private Vector3 FindFirstLineDirection()
    {
        var line = FindNodeRecursive<LineTelegraph3D>(this);
        if (line != null && line.IsActive)
        {
            return line.LockedDirection;
        }

        return Vector3.Zero;
    }

    private static T FindNodeRecursive<T>(Node node)
        where T : Node
    {
        if (node is T match)
        {
            return match;
        }

        for (var index = 0; index < node.GetChildCount(); index++)
        {
            var childMatch = FindNodeRecursive<T>(node.GetChild(index));
            if (childMatch != null)
            {
                return childMatch;
            }
        }

        return null;
    }

    private static int CountActiveLineTelegraphs(Node node)
    {
        var count = node is LineTelegraph3D line && line.IsActive ? 1 : 0;
        for (var index = 0; index < node.GetChildCount(); index++)
        {
            count += CountActiveLineTelegraphs(node.GetChild(index));
        }

        return count;
    }


    private void OnBossAttackStarted(string attackId)
    {
        if (attackId == "molten_ring")
        {
            var ringCount = MetaInt(_boss, "boss_molten_ring_count");
            if (ringCount == 1)
            {
                _ringNormalStartedAt = NowSeconds();
            }
            else if (ringCount == 2)
            {
                ApplyBossChilled("boss_phase_smoke_ring_chilled");
                _ringChilledApplied = true;
                _ringChilledStartedAt = NowSeconds();
            }

            _ringTelegraphObserved = FindActiveRing();
            _ringHealthBefore = _player?.CurrentHealth ?? 0;
        }
        else if (attackId == "ember_barrage")
        {
            var barrageCount = MetaInt(_boss, "boss_ember_barrage_count");
            _observedBarrageCount = barrageCount;
            _barrageTelegraphObserved = ActiveLineTelegraphCount() == 3;
            _barrageDirection0 = FindFirstLineDirection();
            _barrageProjectileCountBefore = EnemyProjectileCount();
            _barrageLaunchCountBefore = MetaInt(_boss, "boss_ember_barrage_launch_count");
            _barrageTargetMoved = false;
            if (barrageCount == 1)
            {
                _barrageNormalStartedAt = NowSeconds();
                _barrageNormalLaunchBefore = _barrageLaunchCountBefore;
            }
            else if (barrageCount == 2)
            {
                ApplyBossChilled("boss_phase_smoke_barrage_chilled");
                _barrageChilledApplied = true;
                _barrageChilledStartedAt = NowSeconds();
                _barrageChilledLaunchBefore = _barrageLaunchCountBefore;
            }
        }
    }

    private void ApplyBossChilled(string sourceId)
    {
        var result = _boss.ApplyDamage(new DamageRequest(
            1,
            DamageType.Physical,
            sourceId,
            CombatFaction.Player,
            false,
            new AilmentApplicationDefinition(AilmentKind.Chilled, 100)));
        if (result.DamageApplied <= 0
            || !_boss.Ailments.Collection.Has(AilmentKind.Chilled))
        {
            throw new InvalidOperationException(
                $"Boss Chilled application failed result={result.DamageApplied} summary={_boss.Ailments.Summary}");
        }
    }

    private static double NowSeconds() => Time.GetTicksMsec() / 1000.0;

    private static void AssertChilledTimingBaseline(string attack, double duration)
    {
        if (duration < 0.1 || duration > 5.0)
        {
            throw new InvalidOperationException(
                $"{attack} timing was not measurable duration={duration:0.000}s");
        }
    }

    private static void AssertChilledActionTiming(
        string attack,
        double normalDuration,
        double chilledDuration)
    {
        AssertChilledTimingBaseline(attack + " chilled", chilledDuration);
        var ratio = chilledDuration / normalDuration;
        var expected = 1.0
            / (1.0 - AilmentCollection.ChilledPotencyPercent / 100.0);
        var minimumExpected = expected - 0.15;
        var maximumExpected = expected + 0.30;
        if (ratio < minimumExpected || ratio > maximumExpected)
        {
            throw new InvalidOperationException(
                $"{attack} Chilled action timing ratio was {ratio:0.000} normal={normalDuration:0.000}s chilled={chilledDuration:0.000}s expected={expected:0.000} range={minimumExpected:0.000}..{maximumExpected:0.000}");
        }
    }

    private int EnemyProjectileCount()
    {
        return CountGroupMembers(this, "enemy_projectiles_3d");
    }

    private static int CountGroupMembers(Node node, string group)
    {
        var count = node.IsInGroup(group) ? 1 : 0;
        for (var index = 0; index < node.GetChildCount(); index++)
        {
            count += CountGroupMembers(node.GetChild(index), group);
        }

        return count;
    }

    private static bool HasMeta(Node node, string key) => node.HasMeta(key);

    private static int MetaInt(Node node, string key) =>
        node.HasMeta(key) ? node.GetMeta(key).AsInt32() : -1;

    private static long MetaLong(Node node, string key) =>
        node.HasMeta(key) ? node.GetMeta(key).AsInt64() : 0;

    private static string MetaString(Node node, string key) =>
        node.HasMeta(key) ? node.GetMeta(key).AsString() : string.Empty;

    private static Vector3 MetaVector3(Node node, string key) =>
        node.HasMeta(key) ? node.GetMeta(key).AsVector3() : Vector3.Zero;

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
