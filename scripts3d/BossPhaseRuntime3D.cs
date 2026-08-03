using System;
using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Pure C# runtime for the phase-two and phase-three Brimstone contract.
/// The map flow owns its physics clock; this class is deliberately not a
/// Godot Node so its state and collections are invisible to C# script
/// discovery and remain map-local through the registry below.
/// </summary>
internal sealed class BossPhaseRuntime3D
{
    private enum AttackState
    {
        Idle,
        RingWindup,
        RingImpact,
        SpearWindup,
        SpearLaunch,
        SlamWindup,
        SlamImpact,
        BarrageWindup,
        BarrageLaunch,
        Recovery,
    }

    private readonly BrimstoneColossusController3D _boss;
    private readonly HealthComponent _health;
    private readonly GameFlowController3D _flow;
    private readonly List<LineTelegraph3D> _activeBarrageTelegraphs = new();
    private readonly List<Vector3> _lockedBarrageDirections = new();

    private BossDefinition _definition;
    private IReadOnlyList<BossPhaseDefinition> _phases = Array.Empty<BossPhaseDefinition>();
    private AttackState _state = AttackState.Idle;
    private RingTelegraph3D _activeRingTelegraph;
    private LineTelegraph3D _activeSpearTelegraph;
    private AreaTelegraph3D _activeSlamTelegraph;
    private BossLavaEruption3D _activeLavaEruption;
    private int _phaseIndex;
    private int _attackPatternIndex;
    private int _phaseTransitionCount;
    private int _bossAddCount;
    private int _ringCount;
    private int _ringImpactCount;
    private int _spearCount;
    private int _spearLaunchCount;
    private int _slamCount;
    private int _slamImpactCount;
    private int _barrageCount;
    private int _barrageLaunchCount;
    private int _lavaCount;
    private float _stateRemaining;
    private float _baseRecoverySeconds;
    private float _recoverySeconds;
    private float _ringDamage;
    private float _ringInnerRadius;
    private float _ringOuterRadius;
    private float _ringPreparationSeconds;
    private float _spearDamage;
    private float _spearPreparationSeconds;
    private float _slamDamage;
    private float _slamRadius;
    private float _slamPreparationSeconds;
    private float _barrageDamage;
    private float _barragePreparationSeconds;
    private float _barrageLength;
    private float _lavaRemaining;
    private Vector3 _lockedRingCenter;
    private Vector3 _lockedSpearDirection = Vector3.Forward;
    private bool _ringImpactApplied;
    private bool _spearLaunchPerformed;
    private bool _slamImpactApplied;
    private bool _barrageLaunchPerformed;
    private bool _initialized;

    internal BossPhaseRuntime3D(BrimstoneColossusController3D boss)
    {
        _boss = boss ?? throw new ArgumentNullException(nameof(boss));
        _health = boss.GetNodeOrNull<HealthComponent>("HealthComponent");
        _flow = FindMapFlow(boss);
        Configure();
    }

    internal void Tick(float delta)
    {
        if (!_initialized)
        {
            return;
        }

        if (!_boss.IsAlive)
        {
            Cancel();
            SetBossMeta("boss_phase_id", "dead");
            SetBossMeta("boss_phase_index", -1);
            return;
        }

        if (_flow != null && _flow.State != GameFlowState.Playing)
        {
            Cancel();
            _state = AttackState.Idle;
            return;
        }

        EvaluatePhase();
        TickLava(delta);
        if (_phaseIndex == 0)
        {
            return;
        }

        switch (_state)
        {
            case AttackState.Idle:
                TryBeginNextAttack();
                break;
            case AttackState.RingWindup:
                TickRingWindup(delta);
                break;
            case AttackState.RingImpact:
                EnterRecovery();
                break;
            case AttackState.SpearWindup:
                TickSpearWindup(delta);
                break;
            case AttackState.SpearLaunch:
                EnterRecovery();
                break;
            case AttackState.SlamWindup:
                TickSlamWindup(delta);
                break;
            case AttackState.SlamImpact:
                EnterRecovery();
                break;
            case AttackState.BarrageWindup:
                TickBarrageWindup(delta);
                break;
            case AttackState.BarrageLaunch:
                EnterRecovery();
                break;
            case AttackState.Recovery:
                _stateRemaining -= Mathf.Max(0.0f, delta);
                if (_stateRemaining <= 0.0f)
                {
                    _state = AttackState.Idle;
                }

                break;
        }
    }

    internal void Cancel()
    {
        _activeRingTelegraph?.Cancel();
        _activeRingTelegraph = null;
        _activeSpearTelegraph?.Cancel();
        _activeSpearTelegraph = null;
        _activeSlamTelegraph?.Cancel();
        _activeSlamTelegraph = null;
        foreach (var telegraph in _activeBarrageTelegraphs)
        {
            telegraph?.Cancel();
        }

        _activeBarrageTelegraphs.Clear();
        if (_activeLavaEruption != null
            && GodotObject.IsInstanceValid(_activeLavaEruption))
        {
            _activeLavaEruption.Cancel();
        }

        _activeLavaEruption = null;
        SetBossMeta("boss_active_barrage_telegraph_count", 0);
    }

    private void Configure()
    {
        var definitionResource = _boss.DefinitionResource;
        _definition = definitionResource?.ToDomain() ?? BossLibrary.BrimstoneColossus();
        _definition.Validate();
        _phases = _definition.Phases ?? Array.Empty<BossPhaseDefinition>();
        if (_phases.Count < 3)
        {
            throw new InvalidOperationException(
                "Brimstone Colossus requires phase-one, phase-two and phase-three definitions.");
        }

        var ring = _definition.Attack(BossAttackKind.MoltenRing);
        var spear = _definition.Attack(BossAttackKind.FlameSpear);
        var slam = _definition.Attack(BossAttackKind.MagmaSlam);
        var barrage = _definition.Attack(BossAttackKind.EmberBarrage);
        _baseRecoverySeconds = (float)_definition.RecoverySeconds;
        _recoverySeconds = _baseRecoverySeconds;
        _ringDamage = _boss.AppliedRingDamage > 0
            ? _boss.AppliedRingDamage
            : ring.Damage;
        _ringPreparationSeconds = Mathf.Max(0.05f, (float)ring.PreparationSeconds);
        _ringOuterRadius = Mathf.Max(3.5f, SpatialScale3D.Distance(ring.Radius));
        _ringInnerRadius = Mathf.Max(0.5f, _ringOuterRadius * 0.5f);
        _spearDamage = _boss.AppliedSecondaryDamage > 0
            ? _boss.AppliedSecondaryDamage
            : spear.Damage;
        _spearPreparationSeconds = Mathf.Max(0.05f, (float)spear.PreparationSeconds);
        _slamDamage = _boss.AppliedPrimaryDamage > 0
            ? _boss.AppliedPrimaryDamage
            : slam.Damage;
        _slamPreparationSeconds = Mathf.Max(0.05f, (float)slam.PreparationSeconds);
        _slamRadius = SpatialScale3D.Distance(slam.Radius);
        _barrageDamage = _boss.AppliedBarrageDamage > 0
            ? _boss.AppliedBarrageDamage
            : barrage.Damage;
        _barragePreparationSeconds = Mathf.Max(0.05f, (float)barrage.PreparationSeconds);
        _barrageLength = Mathf.Max(6.0f, SpatialScale3D.Distance(barrage.Range));
        _phaseIndex = 0;
        _attackPatternIndex = 0;
        _state = AttackState.Idle;
        _initialized = true;
        PublishPhaseMetadata();
    }

    private void EvaluatePhase()
    {
        if (_health == null || _health.MaxHealth <= 0 || _phases.Count == 0)
        {
            return;
        }

        var healthPercent = _health.CurrentHealth * 100.0 / _health.MaxHealth;
        while (_phaseIndex + 1 < _phases.Count
            && healthPercent <= _phases[_phaseIndex + 1].EnterAtHealthPercent)
        {
            EnterPhase(_phaseIndex + 1);
        }
    }

    private void EnterPhase(int phaseIndex)
    {
        if (phaseIndex <= _phaseIndex
            || phaseIndex < 0
            || phaseIndex >= _phases.Count
            || !_boss.IsAlive)
        {
            return;
        }

        _phaseIndex = phaseIndex;
        _attackPatternIndex = 0;
        _state = AttackState.Idle;
        _stateRemaining = 0.0f;
        _recoverySeconds = _baseRecoverySeconds
            * (float)_phases[phaseIndex].RecoveryMultiplier;
        _phaseTransitionCount++;
        Cancel();
        _boss.ActiveSlamTelegraph?.Cancel();
        _boss.ActiveSpearTelegraph?.Cancel();

        if (phaseIndex > 0)
        {
            _boss.SetPhysicsProcess(false);
        }

        var addWaveId = _phases[phaseIndex].AddWaveId;
        if (!string.IsNullOrWhiteSpace(addWaveId))
        {
            var director = FindMapDirector();
            _bossAddCount += director?.TrySpawnBossAdds(addWaveId, 2) ?? 0;
        }

        if (string.Equals(
            _phases[phaseIndex].HazardProfileId,
            "lava-eruption",
            StringComparison.Ordinal))
        {
            _lavaRemaining = 0.65f;
        }

        PublishPhaseMetadata();
        _boss.NotifyBossPhaseChanged(_phaseIndex, _phases[_phaseIndex].Id);
    }

    private void PublishPhaseMetadata()
    {
        SetBossMeta("boss_phase_index", _phaseIndex);
        SetBossMeta("boss_phase_id", _phases.Count > _phaseIndex
            ? _phases[_phaseIndex].Id
            : "phase-1");
        SetBossMeta("boss_phase_count", _phases.Count);
        SetBossMeta("boss_phase_transition_count", _phaseTransitionCount);
        SetBossMeta("boss_add_count", _bossAddCount);
        SetBossMeta("boss_current_attack_id", string.Empty);
        SetBossMeta("boss_active_barrage_telegraph_count", 0);
    }

    private void TryBeginNextAttack()
    {
        if (_phaseIndex <= 0 || _phaseIndex >= _phases.Count)
        {
            return;
        }

        var player = _boss.TargetPlayer;
        if (player == null || !player.IsAlive)
        {
            return;
        }

        var attack = GetCurrentAttack();
        _attackPatternIndex++;
        switch (attack)
        {
            case BossAttackKind.MoltenRing:
                BeginRing();
                break;
            case BossAttackKind.FlameSpear:
                BeginSpear(player);
                break;
            case BossAttackKind.MagmaSlam:
                BeginSlam();
                break;
            case BossAttackKind.EmberBarrage:
                BeginBarrage(player);
                break;
        }
    }

    private BossAttackKind GetCurrentAttack()
    {
        if (_phaseIndex == 1)
        {
            return (_attackPatternIndex % 3) switch
            {
                0 => BossAttackKind.MoltenRing,
                1 => BossAttackKind.FlameSpear,
                _ => BossAttackKind.MagmaSlam,
            };
        }

        return (_attackPatternIndex % 4) switch
        {
            0 => BossAttackKind.EmberBarrage,
            1 => BossAttackKind.MoltenRing,
            2 => BossAttackKind.MagmaSlam,
            _ => BossAttackKind.FlameSpear,
        };
    }

    private void BeginRing()
    {
        _state = AttackState.RingWindup;
        _ringCount++;
        _ringImpactApplied = false;
        _stateRemaining = _ringPreparationSeconds;
        _lockedRingCenter = _boss.GlobalPosition;
        var telegraph = CreateRingTelegraph(
            _lockedRingCenter,
            _ringInnerRadius,
            _ringOuterRadius);
        _activeRingTelegraph = telegraph;
        SetBossMeta("boss_current_attack_id", "molten_ring");
        SetBossMeta("boss_molten_ring_count", _ringCount);
        SetBossMeta("boss_locked_ring_center", _lockedRingCenter);
        SetBossMeta("boss_locked_ring_inner_radius", _ringInnerRadius);
        SetBossMeta("boss_locked_ring_outer_radius", _ringOuterRadius);
        _boss.NotifyBossAttackStarted("molten_ring");
    }

    private void TickRingWindup(float delta)
    {
        _stateRemaining -= delta;
        _activeRingTelegraph?.SetProgress(
            1.0f - _stateRemaining / Mathf.Max(0.01f, _ringPreparationSeconds));
        if (_stateRemaining <= 0.0f)
        {
            ApplyRingImpact();
        }
    }

    private void ApplyRingImpact()
    {
        _state = AttackState.RingImpact;
        _activeRingTelegraph?.Complete();
        _activeRingTelegraph = null;
        if (_ringImpactApplied)
        {
            return;
        }

        _ringImpactApplied = true;
        _ringImpactCount++;
        var player = _boss.TargetPlayer;
        var request = new DamageRequest(
            Mathf.Max(1, Mathf.RoundToInt(_ringDamage)),
            DamageType.Fire,
            "molten_ring_3d",
            CombatFaction.Enemy);
        if (player != null
            && player.IsAlive
            && CombatTargeting.CanHit(request, player)
            && HorizontalDistance(player.GlobalPosition, _lockedRingCenter) >= _ringInnerRadius
            && HorizontalDistance(player.GlobalPosition, _lockedRingCenter) <= _ringOuterRadius)
        {
            player.ApplyDamage(request);
        }

        SetBossMeta("boss_molten_ring_impact_count", _ringImpactCount);
    }

    private void BeginSpear(PlayerController3D player)
    {
        _state = AttackState.SpearWindup;
        _spearCount++;
        _spearLaunchPerformed = false;
        _stateRemaining = _spearPreparationSeconds;
        var direction = player.GlobalPosition - _boss.GlobalPosition;
        direction.Y = 0.0f;
        _lockedSpearDirection = direction.LengthSquared() > 0.001f
            ? direction.Normalized()
            : Vector3.Forward;
        _activeSpearTelegraph = CreateLineTelegraph(
            _boss.GlobalPosition,
            _lockedSpearDirection,
            Mathf.Max(3.0f, direction.Length() + 1.0f),
            _spearPreparationSeconds);
        SetBossMeta("boss_current_attack_id", "flame_spear");
        SetBossMeta("boss_spear_count", _spearCount);
        SetBossMeta("boss_locked_spear_direction", _lockedSpearDirection);
        _boss.NotifyBossAttackStarted("flame_spear");
    }

    private void TickSpearWindup(float delta)
    {
        _stateRemaining -= delta;
        _activeSpearTelegraph?.SetProgress(
            1.0f - _stateRemaining / Mathf.Max(0.01f, _spearPreparationSeconds));
        if (_stateRemaining <= 0.0f)
        {
            LaunchSpear();
        }
    }

    private void LaunchSpear()
    {
        _state = AttackState.SpearLaunch;
        _activeSpearTelegraph?.Complete();
        _activeSpearTelegraph = null;
        if (_spearLaunchPerformed)
        {
            return;
        }

        _spearLaunchPerformed = true;
        var player = _boss.TargetPlayer;
        if (player?.IsAlive == true && _boss.ProjectileScene != null)
        {
            var projectile = _boss.ProjectileScene.Instantiate<BasicProjectile3D>();
            _boss.GetParent()?.AddChild(projectile);
            projectile.GlobalPosition = _boss.GlobalPosition
                + _lockedSpearDirection * 1.4f
                + Vector3.Up * 0.45f;
            projectile.Launch(
                _lockedSpearDirection,
                new DamageRequest(
                    Mathf.Max(1, Mathf.RoundToInt(_spearDamage)),
                    DamageType.Fire,
                    "flame_spear_3d",
                    CombatFaction.Enemy));
            _spearLaunchCount++;
        }

        SetBossMeta("boss_spear_launch_count", _spearLaunchCount);
        SetBossMeta("boss_last_spear_direction", _lockedSpearDirection);
    }

    private void BeginSlam()
    {
        _state = AttackState.SlamWindup;
        _slamCount++;
        _slamImpactApplied = false;
        _stateRemaining = _slamPreparationSeconds;
        var center = _boss.GlobalPosition;
        _activeSlamTelegraph = CreateAreaTelegraph(
            _slamRadius,
            center,
            _slamPreparationSeconds);
        SetBossMeta("boss_current_attack_id", "magma_slam");
        SetBossMeta("boss_slam_count", _slamCount);
        SetBossMeta("boss_locked_slam_center", center);
        SetBossMeta("boss_locked_slam_radius", _slamRadius);
        _boss.NotifyBossAttackStarted("magma_slam");
    }

    private void TickSlamWindup(float delta)
    {
        _stateRemaining -= delta;
        _activeSlamTelegraph?.SetProgress(
            1.0f - _stateRemaining / Mathf.Max(0.01f, _slamPreparationSeconds));
        if (_stateRemaining <= 0.0f)
        {
            ApplySlamImpact();
        }
    }

    private void ApplySlamImpact()
    {
        _state = AttackState.SlamImpact;
        _activeSlamTelegraph?.Complete();
        _activeSlamTelegraph = null;
        if (_slamImpactApplied)
        {
            return;
        }

        _slamImpactApplied = true;
        _slamImpactCount++;
        if (_boss.AreaEffectScene != null && _boss.GetParent() is Node3D map)
        {
            var effect = _boss.AreaEffectScene.Instantiate<SkillAreaEffect3D>();
            map.AddChild(effect);
            effect.ConfigureHazard(
                _boss.GlobalPosition,
                _slamRadius,
                0.0,
                0.18,
                new DamageRequest(
                    Mathf.Max(1, Mathf.RoundToInt(_slamDamage)),
                    DamageType.Fire,
                    "magma_slam_3d",
                    CombatFaction.Enemy));
            effect.SetVisualVisible(false);
            effect.ApplyImpactNow();
            effect.QueueFree();
        }

        SetBossMeta("boss_slam_impact_count", _slamImpactCount);
    }

    private void BeginBarrage(PlayerController3D player)
    {
        _state = AttackState.BarrageWindup;
        _barrageCount++;
        _barrageLaunchPerformed = false;
        _stateRemaining = _barragePreparationSeconds;
        _lockedBarrageDirections.Clear();
        ClearBarrageTelegraphs(false);

        var direction = player.GlobalPosition - _boss.GlobalPosition;
        direction.Y = 0.0f;
        direction = direction.LengthSquared() > 0.001f
            ? direction.Normalized()
            : Vector3.Forward;
        var length = Mathf.Max(
            _barrageLength,
            HorizontalDistance(player.GlobalPosition, _boss.GlobalPosition) + 1.0f);
        for (var index = 0; index < 3; index++)
        {
            var lockedDirection = direction.Rotated(
                Vector3.Up,
                Mathf.DegToRad((index - 1) * 15.0f)).Normalized();
            _lockedBarrageDirections.Add(lockedDirection);
            var telegraph = CreateLineTelegraph(
                _boss.GlobalPosition,
                lockedDirection,
                length,
                _barragePreparationSeconds);
            if (telegraph != null)
            {
                _activeBarrageTelegraphs.Add(telegraph);
            }
        }

        SetBossMeta("boss_current_attack_id", "ember_barrage");
        SetBossMeta("boss_ember_barrage_count", _barrageCount);
        SetBossMeta("boss_active_barrage_telegraph_count", _activeBarrageTelegraphs.Count);
        _boss.NotifyBossAttackStarted("ember_barrage");
    }

    private void TickBarrageWindup(float delta)
    {
        _stateRemaining -= delta;
        var progress = 1.0f - _stateRemaining
            / Mathf.Max(0.01f, _barragePreparationSeconds);
        foreach (var telegraph in _activeBarrageTelegraphs)
        {
            telegraph?.SetProgress(progress);
        }

        if (_stateRemaining <= 0.0f)
        {
            LaunchBarrage();
        }
    }

    private void LaunchBarrage()
    {
        _state = AttackState.BarrageLaunch;
        ClearBarrageTelegraphs(false);
        if (_barrageLaunchPerformed)
        {
            return;
        }

        _barrageLaunchPerformed = true;
        for (var index = 0; index < _lockedBarrageDirections.Count; index++)
        {
            LaunchEmberProjectile(_lockedBarrageDirections[index]);
            SetBossMeta(
                "boss_last_barrage_direction_" + index,
                _lockedBarrageDirections[index]);
        }

        _barrageLaunchCount++;
        SetBossMeta("boss_ember_barrage_launch_count", _barrageLaunchCount);
        SetBossMeta("boss_active_barrage_telegraph_count", 0);
    }

    private void LaunchEmberProjectile(Vector3 direction)
    {
        if (_boss.ProjectileScene == null
            || _boss.TargetPlayer?.IsAlive != true
            || _boss.GetParent() is not Node3D map)
        {
            return;
        }

        var projectile = _boss.ProjectileScene.Instantiate<BasicProjectile3D>();
        map.AddChild(projectile);
        projectile.GlobalPosition = _boss.GlobalPosition
            + direction * 1.4f
            + Vector3.Up * 0.5f;
        projectile.Launch(
            direction,
            new DamageRequest(
                Mathf.Max(1, Mathf.RoundToInt(_barrageDamage)),
                DamageType.Fire,
                "ember_barrage_3d",
                CombatFaction.Enemy));
    }

    private void EnterRecovery()
    {
        _state = AttackState.Recovery;
        _stateRemaining = Mathf.Max(0.01f, _recoverySeconds);
    }

    private void TickLava(float delta)
    {
        if (_phaseIndex < 2
            || _phases.Count <= _phaseIndex
            || !string.Equals(
                _phases[_phaseIndex].HazardProfileId,
                "lava-eruption",
                StringComparison.Ordinal))
        {
            _activeLavaEruption?.Cancel();
            _activeLavaEruption = null;
            return;
        }

        if (_activeLavaEruption != null
            && !GodotObject.IsInstanceValid(_activeLavaEruption))
        {
            _activeLavaEruption = null;
        }

        if (_activeLavaEruption != null)
        {
            return;
        }

        _lavaRemaining -= Mathf.Max(0.0f, delta);
        if (_lavaRemaining > 0.0f)
        {
            return;
        }

        SpawnLava();
        _lavaRemaining = 2.5f;
    }

    private void SpawnLava()
    {
        if (_boss.GetParent() is not Node3D map
            || _boss.TargetPlayer?.IsAlive != true)
        {
            return;
        }

        var run = _boss.RunSession ?? MapRuntimeScope3D.FindRunSession(_boss);
        var seed = RandomService.DeriveSeed(
            run?.CurrentEncounterSeed ?? RandomService.DefaultSeed,
            0x4C41564145525550UL);
        seed = RandomService.DeriveSeed(seed, (ulong)Mathf.Max(0, _boss.SpawnOrdinal));
        seed = RandomService.DeriveSeed(seed, (ulong)(_phaseIndex + 1));
        seed = RandomService.DeriveSeed(seed, (ulong)(_lavaCount + 1));
        var random = new RandomService(seed);
        var angle = (float)(random.NextFloat01() * Mathf.Pi * 2.0);
        var distance = 2.0f + (float)random.NextFloat01() * 3.0f;
        var center = _boss.TargetPlayer.GlobalPosition + new Vector3(
            Mathf.Cos(angle) * distance,
            0.0f,
            Mathf.Sin(angle) * distance);
        var hazard = new BossLavaEruption3D();
        map.AddChild(hazard);
        hazard.Configure(
            center,
            1.35f,
            0.75f,
            Mathf.Max(1, Mathf.RoundToInt(_barrageDamage)),
            DamageType.Fire,
            seed);
        _activeLavaEruption = hazard;
        _lavaCount++;
        SetBossMeta("boss_lava_eruption_count", _lavaCount);
        SetBossMeta(
            "boss_last_hazard_seed",
            (long)(seed & 0x7FFFFFFFFFFFFFFFUL));
    }

    private void ClearBarrageTelegraphs(bool cancel)
    {
        foreach (var telegraph in _activeBarrageTelegraphs)
        {
            if (cancel)
            {
                telegraph?.Cancel();
            }
            else
            {
                telegraph?.Complete();
            }
        }

        _activeBarrageTelegraphs.Clear();
        SetBossMeta("boss_active_barrage_telegraph_count", 0);
    }

    private RingTelegraph3D CreateRingTelegraph(
        Vector3 center,
        float innerRadius,
        float outerRadius)
    {
        if (_boss.GetParent() is not Node3D map)
        {
            return null;
        }

        var scene = GD.Load<PackedScene>("res://scenes3d/RingTelegraph3D.tscn");
        if (scene == null)
        {
            return null;
        }

        var telegraph = scene.Instantiate<RingTelegraph3D>();
        map.AddChild(telegraph);
        telegraph.Activate(
            innerRadius,
            outerRadius,
            center,
            _ringPreparationSeconds);
        return telegraph;
    }

    private AreaTelegraph3D CreateAreaTelegraph(
        float radius,
        Vector3 center,
        float duration)
    {
        if (_boss.GetParent() is not Node3D map)
        {
            return null;
        }

        var scene = GD.Load<PackedScene>("res://scenes3d/AreaTelegraph3D.tscn");
        if (scene == null)
        {
            return null;
        }

        var telegraph = scene.Instantiate<AreaTelegraph3D>();
        map.AddChild(telegraph);
        telegraph.Activate(radius, center, duration);
        return telegraph;
    }

    private LineTelegraph3D CreateLineTelegraph(
        Vector3 start,
        Vector3 direction,
        float length,
        float duration)
    {
        if (_boss.GetParent() is not Node3D map)
        {
            return null;
        }

        var scene = GD.Load<PackedScene>("res://scenes3d/LineTelegraph3D.tscn");
        if (scene == null)
        {
            return null;
        }

        var telegraph = scene.Instantiate<LineTelegraph3D>();
        map.AddChild(telegraph);
        telegraph.Activate(start, direction, length, duration);
        return telegraph;
    }

    private void SetBossMeta(string key, Variant value)
    {
        if (_boss != null)
        {
            _boss.SetMeta(key, value);
        }
    }

    private EncounterDirector3D FindMapDirector()
    {
        for (Node current = _boss; current != null; current = current.GetParent())
        {
            var director = current.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
            if (director != null)
            {
                return director;
            }
        }

        return null;
    }

    private static GameFlowController3D FindMapFlow(Node actor)
    {
        for (var current = actor; current != null; current = current.GetParent())
        {
            var flow = current.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
            if (flow != null)
            {
                return flow;
            }
        }

        return null;
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        var delta = first - second;
        delta.Y = 0.0f;
        return delta.Length();
    }
}

internal static class BossPhaseRuntimeRegistry3D
{
    private static readonly Dictionary<
        BrimstoneColossusController3D,
        BossPhaseRuntime3D> Runtimes = new();

    internal static void Register(BrimstoneColossusController3D boss)
    {
        if (boss == null || Runtimes.ContainsKey(boss))
        {
            return;
        }

        Runtimes.Add(boss, new BossPhaseRuntime3D(boss));
    }

    internal static void TickAll(float delta)
    {
        foreach (var runtime in Runtimes.Values.ToArray())
        {
            runtime.Tick(delta);
        }
    }

    internal static void Remove(BrimstoneColossusController3D boss)
    {
        if (boss != null && Runtimes.Remove(boss, out var runtime))
        {
            runtime.Cancel();
        }
    }
}
