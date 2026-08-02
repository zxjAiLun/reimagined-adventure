using System;
using System.Collections.Generic;
using Arpg.Domain;
using Godot;

public enum BrimstoneColossusState3D
{
    Idle,
    Chasing,
    PreparingSlam,
    SlamImpact,
    Recovering,
    PreparingSpear,
    SpearLaunch,
    PreparingMoltenRing,
    MoltenRingImpact,
    PreparingEmberBarrage,
    EmberBarrageLaunch,
    Dead,
}

/// <summary>
/// 3D presentation adapter for the Domain Brimstone definition. Telegraphs
/// are presentation-only; damage is applied once by the attack impact state.
/// </summary>
public partial class BrimstoneColossusController3D : CharacterBody3D, ICombatTarget, IEnemySpawnConfigurable3D
{
    [Export] public BossDefinitionResource DefinitionResource { get; set; }
    [Export] public float Radius { get; set; } = 1.2f;
    [Export] public PackedScene ProjectileScene { get; set; }
    [Export] public PackedScene AreaEffectScene { get; set; }
    [Export] public PackedScene AreaTelegraphScene { get; set; }
    [Export] public PackedScene LineTelegraphScene { get; set; }
    [Export] public PackedScene RingTelegraphScene { get; set; }
    [Export] public PackedScene ItemDropScene { get; set; }

    [Signal]
    public delegate void BossPhaseChangedEventHandler(int phaseIndex, string phaseId);

    [Signal]
    public delegate void BossAttackStartedEventHandler(string attackId);

    /// <summary>
    /// Compatibility hook for the deterministic scaling smoke. Production
    /// deaths use LootDropProfiles.Boss below.
    /// </summary>
    public bool ForceGuaranteedDropForTest { get; set; }

    public CombatFaction Faction => CombatFaction.Enemy;
    public int CurrentHealth => _health?.CurrentHealth ?? 0;
    public int MaxHealth => _health?.MaxHealth ?? 0;
    public bool IsAlive => _health?.IsAlive ?? false;
    public BrimstoneColossusState3D State { get; private set; } = BrimstoneColossusState3D.Idle;
    public int MagmaSlamCount { get; private set; }
    public int MagmaSlamImpactCount { get; private set; }
    public int FlameSpearCount { get; private set; }
    public int FlameSpearLaunchCount { get; private set; }
    public int MoltenRingCount { get; private set; }
    public int MoltenRingImpactCount { get; private set; }
    public int EmberBarrageCount { get; private set; }
    public int EmberBarrageLaunchCount { get; private set; }
    public int LavaEruptionCount { get; private set; }
    public int PhaseTransitionCount { get; private set; }
    public int BossAddSpawnCount { get; private set; }
    public int CurrentPhaseIndex => _currentPhaseIndex;
    public int PhaseNumber => _phases.Count == 0 ? 1 : _currentPhaseIndex + 1;
    public int PhaseCount => _phases.Count;
    public string CurrentPhaseId => _phases.Count == 0
        ? "phase-1"
        : _phases[Mathf.Clamp(_currentPhaseIndex, 0, _phases.Count - 1)].Id;
    public string CurrentAttackId { get; private set; } = string.Empty;
    public IReadOnlyList<Vector3> LockedBarrageDirections => _lockedBarrageDirections;
    public IReadOnlyList<Vector3> LastBarrageLaunchDirections => _lastBarrageLaunchDirections;
    public float SlamRadius => _slamRadius;
    public EnemyNavigation3D Navigation => _navigation;
    public EnemyCrowdAgent3D CrowdAgent => _crowdAgent;
    public RunSessionNode RunSession => _runSession;
    public PlayerController3D TargetPlayer => _player;
    public AilmentComponent3D Ailments => _ailments;
    public Vector3 LockedSlamCenter { get; private set; }
    public Vector3 LastSlamTelegraphCenter { get; private set; }
    public float LastSlamTelegraphRadius { get; private set; }
    public Vector3 LastSlamImpactCenter { get; private set; }
    public float LastSlamImpactRadius { get; private set; }
    public Vector3 LockedSpearDirection { get; private set; } = Vector3.Forward;
    public Vector3 LastSpearLaunchDirection { get; private set; } = Vector3.Zero;
    public float SpearTelegraphLength { get; private set; }
    public AreaTelegraph3D ActiveSlamTelegraph => _activeSlamTelegraph;
    public LineTelegraph3D ActiveSpearTelegraph => _activeSpearTelegraph;
    public RingTelegraph3D ActiveRingTelegraph => _activeRingTelegraph;
    public int ActiveBarrageTelegraphCount => _activeBarrageTelegraphs.Count;
    public BossLavaEruption3D ActiveLavaEruption => _activeLavaEruption;
    public ulong LastHazardSeed { get; private set; }
    public float RingInnerRadius => _ringInnerRadius;
    public float RingOuterRadius => _ringOuterRadius;
    public Vector3 LockedRingCenter { get; private set; }
    public int AppliedMapLevel { get; private set; } = 1;
    public int AppliedMaxHealth { get; private set; }
    public int AppliedPrimaryDamage { get; private set; }
    public int AppliedSecondaryDamage { get; private set; }
    public int AppliedDropItemLevel { get; private set; } = 1;
    public string SpawnEncounterId { get; private set; } = string.Empty;
    public string SpawnWaveId { get; private set; } = string.Empty;
    public int SpawnOrdinal { get; private set; }
    public int SpawnContextAppliedCount { get; private set; }
    public bool ExperienceAwarded { get; private set; }
    public float MoveSpeed => _moveSpeed;
    public float MagmaSlamPreparationSeconds => _slamPreparationSeconds;
    public float FlameSpearPreparationSeconds => _spearPreparationSeconds;
    public float RecoverySeconds => _recoverySeconds;

    private HealthComponent _health;
    private DamageFeedbackSource3D _damageFeedback;
    private HitFlash3D _hitFlash;
    private DeathFeedback3D _deathFeedback;
    private AilmentComponent3D _ailments;
    private PlayerController3D _player;
    private RunSessionNode _runSession;
    private Label3D _healthLabel;
    private AreaTelegraph3D _activeSlamTelegraph;
    private LineTelegraph3D _activeSpearTelegraph;
    private RingTelegraph3D _activeRingTelegraph;
    private readonly List<LineTelegraph3D> _activeBarrageTelegraphs = new();
    private readonly List<Vector3> _lockedBarrageDirections = new();
    private readonly List<Vector3> _lastBarrageLaunchDirections = new();
    private BossDefinition _bossDefinition;
    private IReadOnlyList<BossPhaseDefinition> _phases = Array.Empty<BossPhaseDefinition>();
    private int _currentPhaseIndex;
    private int _attackPatternIndex;
    private float _baseRecoverySeconds;
    private float _moveSpeed;
    private float _slamDamage;
    private float _slamPreparationSeconds;
    private float _slamRadius;
    private float _slamRange;
    private float _spearDamage;
    private float _spearPreparationSeconds;
    private float _spearRange;
    private float _recoverySeconds;
    private float _ringDamage;
    private float _ringInnerRadius;
    private float _ringOuterRadius;
    private float _ringPreparationSeconds;
    private float _barrageDamage;
    private float _barragePreparationSeconds;
    private float _barrageRange;
    private float _barrageLength;
    private int _scaledRingDamage;
    private int _scaledBarrageDamage;
    private float _lavaRemaining;
    private int _lavaActivationCount;
    private BossLavaEruption3D _activeLavaEruption;
    private bool _ringImpactApplied;
    private bool _barrageLaunchPerformed;
    private float _stateRemaining;
    private bool _nextAttackIsSlam = true;
    private bool _slamImpactApplied;
    private bool _spearLaunchPerformed;
    private bool _deathHandled;
    private EnemyNavigation3D _navigation;
    private EnemyCrowdAgent3D _crowdAgent;
    private EnemySpawnContext3D _spawnContext;
    private bool _spawnContextApplied;
    private CombatFaction _lastPositiveDamageSourceFaction = CombatFaction.Neutral;

    public void ConfigureBeforeReady(EnemySpawnContext3D context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_spawnContextApplied)
        {
            throw new InvalidOperationException("Boss spawn context was already applied.");
        }

        context.Validate();
        if (!context.IsBoss)
        {
            throw new InvalidOperationException("Brimstone Colossus requires a boss spawn context.");
        }

        var definition = DefinitionResource?.ToDomain() ?? BossLibrary.BrimstoneColossus();
        var health = GetNodeOrNull<HealthComponent>("HealthComponent")
            ?? throw new InvalidOperationException("Brimstone Colossus is missing HealthComponent.");
        var modifier = context.MapModifier;
        var bossScaling = new BossScalingProfile
        {
            HpMultiplier = 1.0,
            DamageBonus = 0,
        };
        var scaledHealth = MapScaling.BossHp(definition.MaxHealth, context.MapLevel, modifier, bossScaling);
        var scaledSlamDamage = MapScaling.BossContactDamage(
            definition.Attack(BossAttackKind.MagmaSlam).Damage,
            context.MapLevel,
            modifier,
            bossScaling);
        var scaledSpearDamage = MapScaling.BossContactDamage(
            definition.Attack(BossAttackKind.FlameSpear).Damage,
            context.MapLevel,
            modifier,
            bossScaling);
        var scaledRingDamage = MapScaling.BossContactDamage(
            definition.Attack(BossAttackKind.MoltenRing).Damage,
            context.MapLevel,
            modifier,
            bossScaling);
        var scaledBarrageDamage = MapScaling.BossContactDamage(
            definition.Attack(BossAttackKind.EmberBarrage).Damage,
            context.MapLevel,
            modifier,
            bossScaling);
        health.SetMaxHealth(scaledHealth);

        var agent = GetNodeOrNull<NavigationAgent3D>("NavigationAgent3D");
        if (agent != null)
        {
            agent.NavigationLayers = (uint)context.NavigationLayers;
        }

        _spawnContext = context;
        _spawnContextApplied = true;
        SpawnContextAppliedCount = 1;
        AppliedMapLevel = context.MapLevel;
        AppliedMaxHealth = scaledHealth;
        AppliedPrimaryDamage = scaledSlamDamage;
        AppliedSecondaryDamage = scaledSpearDamage;
        _scaledRingDamage = scaledRingDamage;
        _scaledBarrageDamage = scaledBarrageDamage;
        AppliedDropItemLevel = context.DropItemLevel;
        SpawnEncounterId = context.EncounterId;
        SpawnWaveId = context.WaveId;
        SpawnOrdinal = context.SpawnOrdinal;
    }

    public override void _Ready()
    {
        AddToGroup("damageables_3d");
        AddToGroup("enemies_3d");
        AddToGroup("bosses_3d");
        _health = GetNode<HealthComponent>("HealthComponent");
        _damageFeedback = GetNodeOrNull<DamageFeedbackSource3D>("DamageFeedbackSource3D");
        _hitFlash = GetNodeOrNull<HitFlash3D>("HitFlash3D");
        _deathFeedback = GetNodeOrNull<DeathFeedback3D>("DeathFeedback3D");
        _ailments = GetNodeOrNull<AilmentComponent3D>("AilmentComponent3D");
        _health.Died += OnDied;
        _health.DamageTaken += OnDamageTaken;
        _health.HealthChanged += OnHealthChanged;
        _healthLabel = GetNodeOrNull<Label3D>("HealthLabel");
        _navigation = GetNodeOrNull<EnemyNavigation3D>("EnemyNavigation3D");
        _crowdAgent = GetNodeOrNull<EnemyCrowdAgent3D>("EnemyCrowdAgent3D");
        _runSession = _spawnContext?.RunSession ?? MapRuntimeScope3D.FindRunSession(this);
        ApplyDefinition(DefinitionResource?.ToDomain() ?? BossLibrary.BrimstoneColossus());
        if (!_spawnContextApplied)
        {
            AppliedMaxHealth = _health.MaxHealth;
            AppliedPrimaryDamage = Mathf.RoundToInt(_slamDamage);
            AppliedSecondaryDamage = Mathf.RoundToInt(_spearDamage);
            SpawnOrdinal = 0;
        }

        _player = _spawnContext?.Player;
        _player ??= MapRuntimeScope3D.FindPlayer(this);
        InitializePhaseRuntime();
        RefreshVisuals();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsAlive || State == BrimstoneColossusState3D.Dead)
        {
            return;
        }

        if (_player == null || !GodotObject.IsInstanceValid(_player))
        {
            FindPlayer();
        }

        if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsAlive)
        {
            CancelTelegraphs();
            Velocity = Vector3.Zero;
            return;
        }

        var frameDelta = (float)delta;
        TickLavaEruption(frameDelta);
        switch (State)
        {
            case BrimstoneColossusState3D.Idle:
            case BrimstoneColossusState3D.Chasing:
                TryChooseAttack(frameDelta);
                break;
            case BrimstoneColossusState3D.PreparingSlam:
                Velocity = Vector3.Zero;
                _stateRemaining -= frameDelta * (float)(_ailments?.ActionSpeedMultiplier ?? 1.0);
                _activeSlamTelegraph?.SetProgress(
                    1.0f - _stateRemaining / Mathf.Max(0.01f, _slamPreparationSeconds));
                if (_stateRemaining <= 0.0f)
                {
                    BeginSlamImpact();
                }

                break;
            case BrimstoneColossusState3D.SlamImpact:
                Velocity = Vector3.Zero;
                State = BrimstoneColossusState3D.Recovering;
                _stateRemaining = Mathf.Max(0.01f, _recoverySeconds);
                break;
            case BrimstoneColossusState3D.PreparingSpear:
                Velocity = Vector3.Zero;
                _stateRemaining -= frameDelta * (float)(_ailments?.ActionSpeedMultiplier ?? 1.0);
                _activeSpearTelegraph?.SetProgress(
                    1.0f - _stateRemaining / Mathf.Max(0.01f, _spearPreparationSeconds));
                if (_stateRemaining <= 0.0f)
                {
                    BeginSpearLaunch();
                }

                break;
            case BrimstoneColossusState3D.SpearLaunch:
                Velocity = Vector3.Zero;
                State = BrimstoneColossusState3D.Recovering;
                _stateRemaining = Mathf.Max(0.01f, _recoverySeconds);
                break;
            case BrimstoneColossusState3D.PreparingMoltenRing:
                Velocity = Vector3.Zero;
                _stateRemaining -= frameDelta * (float)(_ailments?.ActionSpeedMultiplier ?? 1.0);
                _activeRingTelegraph?.SetProgress(
                    1.0f - _stateRemaining / Mathf.Max(0.01f, _ringPreparationSeconds));
                if (_stateRemaining <= 0.0f)
                {
                    BeginMoltenRingImpact();
                }

                break;
            case BrimstoneColossusState3D.MoltenRingImpact:
                Velocity = Vector3.Zero;
                State = BrimstoneColossusState3D.Recovering;
                _stateRemaining = Mathf.Max(0.01f, _recoverySeconds);
                break;
            case BrimstoneColossusState3D.PreparingEmberBarrage:
                Velocity = Vector3.Zero;
                _stateRemaining -= frameDelta * (float)(_ailments?.ActionSpeedMultiplier ?? 1.0);
                foreach (var telegraph in _activeBarrageTelegraphs)
                {
                    telegraph?.SetProgress(
                        1.0f - _stateRemaining / Mathf.Max(0.01f, _barragePreparationSeconds));
                }

                if (_stateRemaining <= 0.0f)
                {
                    BeginEmberBarrageLaunch();
                }

                break;
            case BrimstoneColossusState3D.EmberBarrageLaunch:
                Velocity = Vector3.Zero;
                State = BrimstoneColossusState3D.Recovering;
                _stateRemaining = Mathf.Max(0.01f, _recoverySeconds);
                break;
            case BrimstoneColossusState3D.Recovering:
                Velocity = Vector3.Zero;
                _stateRemaining -= frameDelta * (float)(_ailments?.ActionSpeedMultiplier ?? 1.0);
                if (_stateRemaining <= 0.0f)
                {
                    State = BrimstoneColossusState3D.Idle;
                    CurrentAttackId = string.Empty;
                }

                break;
            case BrimstoneColossusState3D.Dead:
                break;
        }

        GlobalPosition = new Vector3(GlobalPosition.X, 0.0f, GlobalPosition.Z);
        RefreshVisuals();
    }

    public DamageResult ApplyDamage(DamageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsAlive)
        {
            return new DamageResult(0, false);
        }

        var incomingRequest = _ailments?.ModifyIncomingDamage(request) ?? request;
        var result = _health.ApplyDamage(incomingRequest);
        if (result.DamageApplied > 0)
        {
            _lastPositiveDamageSourceFaction = request.SourceFaction;
            _damageFeedback?.Publish(result);
            _hitFlash?.Trigger();
            _ailments?.ApplyFromDamage(request, result.DamageApplied);
        }

        RefreshVisuals();
        return result;
    }

    private void OnDamageTaken(DamageRequest request, DamageResult result)
    {
        if (result.DamageApplied > 0)
        {
            _lastPositiveDamageSourceFaction = request.SourceFaction;
        }
    }

    private void OnHealthChanged(int currentHealth, int maxHealth)
    {
        if (!IsAlive || _phases.Count == 0 || maxHealth <= 0)
        {
            return;
        }

        var healthPercent = currentHealth * 100.0 / maxHealth;
        while (_currentPhaseIndex + 1 < _phases.Count
            && healthPercent <= _phases[_currentPhaseIndex + 1].EnterAtHealthPercent)
        {
            EnterPhase(_currentPhaseIndex + 1);
        }
    }

    private void FindPlayer()
    {
        _player = MapRuntimeScope3D.FindPlayer(this);
    }

    private void TryChooseAttack(float frameDelta)
    {
        var toPlayer = _player.GlobalPosition - GlobalPosition;
        toPlayer.Y = 0.0f;
        var distance = toPlayer.Length();
        if (TryGetNextAttack(out var attackKind)
            && IsAttackInRange(attackKind, distance))
        {
            switch (attackKind)
            {
                case BossAttackKind.MagmaSlam:
                    BeginMagmaSlam();
                    return;
                case BossAttackKind.FlameSpear:
                    BeginFlameSpear();
                    return;
                case BossAttackKind.MoltenRing:
                    BeginMoltenRing();
                    return;
                case BossAttackKind.EmberBarrage:
                    BeginEmberBarrage();
                    return;
            }
        }

        State = BrimstoneColossusState3D.Chasing;
        if (_navigation == null)
        {
            Velocity = Vector3.Zero;
            return;
        }

        var previousPosition = GlobalPosition;
        _navigation.SetTarget(_player.GlobalPosition);
        var direction = _navigation.GetDesiredDirection(GlobalPosition, frameDelta);
        if (!_navigation.IsNavigationReady
            || !_navigation.HasPath
            || !_navigation.IsTargetReachable)
        {
            Velocity = Vector3.Zero;
            _navigation.NotifyMovement(previousPosition, GlobalPosition, frameDelta);
            return;
        }

        var navigationVelocity = direction.LengthSquared() > 0.001f
            ? direction * _moveSpeed
            : Vector3.Zero;
        Velocity = _crowdAgent?.CombineNavigationVelocity(navigationVelocity, _moveSpeed)
            ?? navigationVelocity;
        if (direction.LengthSquared() > 0.001f)
        {
            MoveAndSlide();
        }

        _navigation.NotifyMovement(previousPosition, GlobalPosition, frameDelta);
        _crowdAgent?.NotifyMovement(
            _navigation,
            direction,
            previousPosition,
            GlobalPosition,
            frameDelta);
    }

    private bool TryGetNextAttack(out BossAttackKind attackKind)
    {
        if (_phases.Count > 0)
        {
            var pattern = _phases[_currentPhaseIndex].AttackPattern;
            attackKind = pattern[Mathf.Clamp(_attackPatternIndex, 0, pattern.Count - 1)];
            return true;
        }

        attackKind = _nextAttackIsSlam
            ? BossAttackKind.MagmaSlam
            : BossAttackKind.FlameSpear;
        return true;
    }

    private bool IsAttackInRange(BossAttackKind attackKind, float distance)
    {
        return attackKind switch
        {
            BossAttackKind.MagmaSlam => distance <= _slamRange,
            BossAttackKind.FlameSpear => distance <= _spearRange,
            BossAttackKind.MoltenRing => distance <= Mathf.Max(_slamRange, _ringOuterRadius + 3.0f),
            BossAttackKind.EmberBarrage => distance <= _barrageRange,
            _ => false,
        };
    }

    private void AdvanceAttackPattern()
    {
        if (_phases.Count > 0)
        {
            var pattern = _phases[_currentPhaseIndex].AttackPattern;
            _attackPatternIndex = (_attackPatternIndex + 1) % pattern.Count;
        }
    }

    private void MarkAttackStarted(string attackId)
    {
        CurrentAttackId = attackId;
        EmitSignal(SignalName.BossAttackStarted, attackId);
    }

    private void BeginMagmaSlam()
    {
        State = BrimstoneColossusState3D.PreparingSlam;
        MagmaSlamCount++;
        _slamImpactApplied = false;
        _stateRemaining = Mathf.Max(0.05f, _slamPreparationSeconds);
        _nextAttackIsSlam = false;
        LockedSlamCenter = GlobalPosition;
        LastSlamTelegraphCenter = LockedSlamCenter;
        LastSlamTelegraphRadius = _slamRadius;
        AdvanceAttackPattern();
        MarkAttackStarted("magma_slam");
        _activeSlamTelegraph = CreateAreaTelegraph(
            _slamRadius,
            LockedSlamCenter,
            _slamPreparationSeconds);
    }

    private void BeginSlamImpact()
    {
        State = BrimstoneColossusState3D.SlamImpact;
        _activeSlamTelegraph?.Complete();
        _activeSlamTelegraph = null;
        if (_slamImpactApplied)
        {
            return;
        }

        _slamImpactApplied = true;
        MagmaSlamImpactCount++;
        LastSlamImpactCenter = new Vector3(LockedSlamCenter.X, 0.04f, LockedSlamCenter.Z);
        LastSlamImpactRadius = _slamRadius;
        if (AreaEffectScene == null || GetParent() == null)
        {
            return;
        }

        var effect = AreaEffectScene.Instantiate<SkillAreaEffect3D>();
        GetParent().AddChild(effect);
        effect.ConfigureHazard(
            LockedSlamCenter,
            _slamRadius,
            0.0,
            0.18,
            new DamageRequest(
                (int)_slamDamage,
                DamageType.Fire,
                "magma_slam_3d",
                CombatFaction.Enemy));
        effect.SetVisualVisible(false);
        effect.ApplyImpactNow();
        effect.QueueFree();
    }

    private void BeginFlameSpear()
    {
        State = BrimstoneColossusState3D.PreparingSpear;
        FlameSpearCount++;
        _spearLaunchPerformed = false;
        _stateRemaining = Mathf.Max(0.05f, _spearPreparationSeconds);
        _nextAttackIsSlam = true;
        var direction = _player.GlobalPosition - GlobalPosition;
        direction.Y = 0.0f;
        LockedSpearDirection = direction.LengthSquared() > 0.001f
            ? direction.Normalized()
            : Vector3.Forward;
        SpearTelegraphLength = Mathf.Max(3.0f, direction.Length() + 1.0f);
        AdvanceAttackPattern();
        MarkAttackStarted("flame_spear");
        _activeSpearTelegraph = CreateLineTelegraph(
            LockedSpearDirection,
            SpearTelegraphLength,
            _spearPreparationSeconds);
    }

    private void BeginSpearLaunch()
    {
        State = BrimstoneColossusState3D.SpearLaunch;
        _activeSpearTelegraph?.Complete();
        _activeSpearTelegraph = null;
        if (!_spearLaunchPerformed)
        {
            _spearLaunchPerformed = true;
            FireFlameSpear();
        }

        _stateRemaining = 0.0f;
    }

    private void BeginMoltenRing()
    {
        State = BrimstoneColossusState3D.PreparingMoltenRing;
        MoltenRingCount++;
        _ringImpactApplied = false;
        _stateRemaining = Mathf.Max(0.05f, _ringPreparationSeconds);
        LockedRingCenter = GlobalPosition;
        AdvanceAttackPattern();
        MarkAttackStarted("molten_ring");
        _activeRingTelegraph = CreateRingTelegraph(
            _ringInnerRadius,
            _ringOuterRadius,
            LockedRingCenter,
            _ringPreparationSeconds);
    }

    private void BeginMoltenRingImpact()
    {
        State = BrimstoneColossusState3D.MoltenRingImpact;
        _activeRingTelegraph?.Complete();
        _activeRingTelegraph = null;
        if (_ringImpactApplied)
        {
            return;
        }

        _ringImpactApplied = true;
        MoltenRingImpactCount++;
        var request = new DamageRequest(
            Mathf.Max(1, Mathf.RoundToInt(_ringDamage)),
            DamageType.Fire,
            "molten_ring_3d",
            CombatFaction.Enemy);
        foreach (var node in GetTree().GetNodesInGroup("damageables_3d"))
        {
            if (node is not Node3D damageableNode
                || node is not ICombatTarget target
                || !target.IsAlive
                || !CombatTargeting.CanHit(request, target))
            {
                continue;
            }

            var distance = HorizontalDistance(damageableNode.GlobalPosition, LockedRingCenter);
            if (distance >= _ringInnerRadius && distance <= _ringOuterRadius)
            {
                target.ApplyDamage(request);
            }
        }
    }

    private void BeginEmberBarrage()
    {
        State = BrimstoneColossusState3D.PreparingEmberBarrage;
        EmberBarrageCount++;
        _barrageLaunchPerformed = false;
        _stateRemaining = Mathf.Max(0.05f, _barragePreparationSeconds);
        _lockedBarrageDirections.Clear();
        _lastBarrageLaunchDirections.Clear();
        CancelBarrageTelegraphs();

        var direction = _player.GlobalPosition - GlobalPosition;
        direction.Y = 0.0f;
        direction = direction.LengthSquared() > 0.001f
            ? direction.Normalized()
            : Vector3.Forward;
        var length = Mathf.Max(_barrageLength, (float)HorizontalDistance(_player.GlobalPosition, GlobalPosition) + 1.0f);
        foreach (var degrees in new[] { -15.0f, 0.0f, 15.0f })
        {
            var lockedDirection = direction.Rotated(Vector3.Up, Mathf.DegToRad(degrees)).Normalized();
            _lockedBarrageDirections.Add(lockedDirection);
            var telegraph = CreateLineTelegraph(
                GlobalPosition,
                lockedDirection,
                length,
                _barragePreparationSeconds);
            if (telegraph != null)
            {
                _activeBarrageTelegraphs.Add(telegraph);
            }
        }

        AdvanceAttackPattern();
        MarkAttackStarted("ember_barrage");
    }

    private void BeginEmberBarrageLaunch()
    {
        State = BrimstoneColossusState3D.EmberBarrageLaunch;
        CompleteBarrageTelegraphs();
        if (_barrageLaunchPerformed)
        {
            return;
        }

        _barrageLaunchPerformed = true;
        _lastBarrageLaunchDirections.AddRange(_lockedBarrageDirections);
        foreach (var direction in _lockedBarrageDirections)
        {
            FireEmberProjectile(direction);
        }

        EmberBarrageLaunchCount++;
        _stateRemaining = 0.0f;
    }

    private AreaTelegraph3D CreateAreaTelegraph(float radius, Vector3 position, float duration)
    {
        if (AreaTelegraphScene == null || GetParent() == null)
        {
            return null;
        }

        var telegraph = AreaTelegraphScene.Instantiate<AreaTelegraph3D>();
        GetParent().AddChild(telegraph);
        telegraph.Activate(radius, position, duration);
        return telegraph;
    }

    private LineTelegraph3D CreateLineTelegraph(Vector3 direction, float length, float duration)
    {
        return CreateLineTelegraph(GlobalPosition, direction, length, duration);
    }

    private LineTelegraph3D CreateLineTelegraph(
        Vector3 origin,
        Vector3 direction,
        float length,
        float duration)
    {
        if (LineTelegraphScene == null || GetParent() == null)
        {
            return null;
        }

        var telegraph = LineTelegraphScene.Instantiate<LineTelegraph3D>();
        GetParent().AddChild(telegraph);
        telegraph.Activate(origin, direction, length, duration);
        return telegraph;
    }

    private RingTelegraph3D CreateRingTelegraph(
        float innerRadius,
        float outerRadius,
        Vector3 position,
        float duration)
    {
        if (RingTelegraphScene == null || GetParent() == null)
        {
            return null;
        }

        var telegraph = RingTelegraphScene.Instantiate<RingTelegraph3D>();
        GetParent().AddChild(telegraph);
        telegraph.Activate(innerRadius, outerRadius, position, duration);
        return telegraph;
    }

    private void FireFlameSpear()
    {
        if (ProjectileScene == null || !_player.IsAlive || GetParent() == null)
        {
            return;
        }

        var projectile = ProjectileScene.Instantiate<BasicProjectile3D>();
        GetParent().AddChild(projectile);
        projectile.GlobalPosition = GlobalPosition
            + LockedSpearDirection * (Radius + 0.2f)
            + Vector3.Up * 0.45f;
        projectile.Launch(
            LockedSpearDirection,
            new DamageRequest(
                (int)_spearDamage,
                DamageType.Fire,
                "flame_spear_3d",
                CombatFaction.Enemy));
        LastSpearLaunchDirection = projectile.LaunchDirection;
        FlameSpearLaunchCount++;
    }

    private void FireEmberProjectile(Vector3 direction)
    {
        if (ProjectileScene == null || !_player.IsAlive || GetParent() == null)
        {
            return;
        }

        var projectile = ProjectileScene.Instantiate<BasicProjectile3D>();
        GetParent().AddChild(projectile);
        projectile.GlobalPosition = GlobalPosition
            + direction * (Radius + 0.2f)
            + Vector3.Up * 0.5f;
        projectile.Launch(
            direction,
            new DamageRequest(
                Mathf.Max(1, Mathf.RoundToInt(_barrageDamage)),
                DamageType.Fire,
                "ember_barrage_3d",
                CombatFaction.Enemy));
    }

    private void CompleteBarrageTelegraphs()
    {
        foreach (var telegraph in _activeBarrageTelegraphs)
        {
            telegraph?.Complete();
        }

        _activeBarrageTelegraphs.Clear();
    }

    private void CancelBarrageTelegraphs()
    {
        foreach (var telegraph in _activeBarrageTelegraphs)
        {
            telegraph?.Cancel();
        }

        _activeBarrageTelegraphs.Clear();
    }

    private void CancelTelegraphs()
    {
        _activeSlamTelegraph?.Cancel();
        _activeSlamTelegraph = null;
        _activeSpearTelegraph?.Cancel();
        _activeSpearTelegraph = null;
        _activeRingTelegraph?.Cancel();
        _activeRingTelegraph = null;
        CancelBarrageTelegraphs();
    }

    private void InitializePhaseRuntime()
    {
        _currentPhaseIndex = 0;
        _attackPatternIndex = 0;
        _baseRecoverySeconds = _recoverySeconds;
        _lavaRemaining = 0.0f;
        CurrentAttackId = string.Empty;
        if (_phases.Count > 0)
        {
            EmitSignal(SignalName.BossPhaseChanged, 0, _phases[0].Id);
        }
    }

    private void EnterPhase(int phaseIndex)
    {
        if (!IsAlive || phaseIndex < 0 || phaseIndex >= _phases.Count)
        {
            return;
        }

        _currentPhaseIndex = phaseIndex;
        _attackPatternIndex = 0;
        _recoverySeconds = _baseRecoverySeconds
            * (float)_phases[phaseIndex].RecoveryMultiplier;
        PhaseTransitionCount++;
        var phase = _phases[phaseIndex];
        if (!string.IsNullOrWhiteSpace(phase.AddWaveId))
        {
            var director = GetParent()?.GetParent()?.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
            BossAddSpawnCount += director?.TrySpawnBossAdds(phase.AddWaveId, 2) ?? 0;
        }

        if (string.Equals(phase.HazardProfileId, "lava-eruption", StringComparison.Ordinal))
        {
            _lavaRemaining = 0.65f;
        }

        EmitSignal(SignalName.BossPhaseChanged, phaseIndex, phase.Id);
    }

    private void TickLavaEruption(float delta)
    {
        if (_phases.Count == 0
            || _currentPhaseIndex < 0
            || _currentPhaseIndex >= _phases.Count
            || !string.Equals(
                _phases[_currentPhaseIndex].HazardProfileId,
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

        SpawnLavaEruption();
        _lavaRemaining = 2.5f;
    }

    private void SpawnLavaEruption()
    {
        var map = GetParent()?.GetParent() as Node3D;
        if (map == null || _player == null || !_player.IsAlive)
        {
            return;
        }

        var seed = RandomService.DeriveSeed(
            _runSession?.CurrentEncounterSeed ?? RandomService.DefaultSeed,
            0x4C41564145525550UL);
        seed = RandomService.DeriveSeed(seed, (ulong)Mathf.Max(0, SpawnOrdinal));
        seed = RandomService.DeriveSeed(seed, (ulong)(_currentPhaseIndex + 1));
        seed = RandomService.DeriveSeed(seed, (ulong)(_lavaActivationCount + 1));
        var random = new RandomService(seed);
        var angle = random.NextFloat01() * Mathf.Pi * 2.0;
        var distance = 2.0 + random.NextFloat01() * 3.0;
        var center = _player.GlobalPosition + new Vector3(
            (float)Math.Cos(angle) * (float)distance,
            0.0f,
            (float)Math.Sin(angle) * (float)distance);
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
        _lavaActivationCount++;
        LavaEruptionCount++;
        LastHazardSeed = seed;
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        var delta = first - second;
        delta.Y = 0.0f;
        return delta.Length();
    }

    private void OnDied()
    {
        if (_deathHandled)
        {
            return;
        }

        _deathHandled = true;
        CancelTelegraphs();
        _activeLavaEruption?.Cancel();
        _activeLavaEruption = null;
        State = BrimstoneColossusState3D.Dead;
        _crowdAgent?.SetActive(false);
        Velocity = Vector3.Zero;
        CollisionLayer = 0;
        CollisionMask = 0;
        SetPhysicsProcess(false);
        _deathFeedback?.Play();
        AwardExperienceIfEligible();
        SpawnDrop();
        RefreshVisuals();
    }

    private void AwardExperienceIfEligible()
    {
        if (ExperienceAwarded
            || _lastPositiveDamageSourceFaction != CombatFaction.Player)
        {
            return;
        }

        _runSession ??= MapRuntimeScope3D.FindRunSession(this);
        _player ??= MapRuntimeScope3D.FindPlayer(this);
        var map = GetParent()?.GetParent();
        var flow = map?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        if (_runSession == null || _player?.IsAlive != true || flow?.State != GameFlowState.Playing)
        {
            return;
        }

        var sourceId = string.IsNullOrWhiteSpace(SpawnEncounterId)
            ? $"boss:{GetPath()}"
            : $"boss:{SpawnEncounterId}:{SpawnWaveId}:{SpawnOrdinal}";
        ExperienceAwarded = _runSession.TryAwardExperience(ExperienceSourceKind.Boss, sourceId);
    }

    private void SpawnDrop()
    {
        if (ItemDropScene == null || GetParent() == null)
        {
            return;
        }

        _runSession ??= MapRuntimeScope3D.FindRunSession(this);
        if (_runSession == null)
        {
            GD.PushError("BrimstoneColossus3D cannot drop loot without a RunSessionNode.");
            return;
        }

        if (ForceGuaranteedDropForTest)
        {
            SpawnDropItem(_runSession.GenerateWeaponDrop(AppliedDropItemLevel, boss: true));
            return;
        }

        var result = _runSession.GenerateDrops(
            new ItemRollContext(AppliedDropItemLevel, LootSourceKind.Boss, GuaranteedUnique: true),
            LootDropProfiles.Boss);
        _runSession.TryAwardForgeFragments(result.ForgeFragments);
        foreach (var item in result.Items)
        {
            SpawnDropItem(item);
        }
    }

    private void SpawnDropItem(Item item)
    {
        var drop = ItemDropScene?.Instantiate<ItemDrop3D>();
        if (drop == null || GetParent() == null)
        {
            return;
        }

        GetParent().AddChild(drop);
        drop.GlobalPosition = GlobalPosition;
        drop.Configure(item);
    }

    private void RefreshVisuals()
    {
        if (_healthLabel != null)
        {
            _healthLabel.Text = $"BRIMSTONE COLOSSUS {CurrentHealth}/{MaxHealth}\n{State}";
        }
    }

    private void ApplyDefinition(BossDefinition definition)
    {
        definition.Validate();
        _bossDefinition = definition;
        _phases = definition.Phases ?? Array.Empty<BossPhaseDefinition>();
        _moveSpeed = SpatialScale3D.Distance(definition.MoveSpeed);
        _recoverySeconds = (float)definition.RecoverySeconds;
        _baseRecoverySeconds = _recoverySeconds;
        var slam = definition.Attack(BossAttackKind.MagmaSlam);
        var spear = definition.Attack(BossAttackKind.FlameSpear);
        var ring = definition.Attack(BossAttackKind.MoltenRing);
        var barrage = definition.Attack(BossAttackKind.EmberBarrage);
        _slamDamage = _spawnContextApplied ? AppliedPrimaryDamage : slam.Damage;
        _slamPreparationSeconds = (float)slam.PreparationSeconds;
        _slamRadius = SpatialScale3D.Distance(slam.Radius);
        _slamRange = SpatialScale3D.Distance(slam.Range);
        _spearDamage = _spawnContextApplied ? AppliedSecondaryDamage : spear.Damage;
        _spearPreparationSeconds = (float)spear.PreparationSeconds;
        _spearRange = SpatialScale3D.Distance(spear.Range);
        _ringDamage = _spawnContextApplied && _scaledRingDamage > 0
            ? _scaledRingDamage
            : ring.Damage;
        _ringPreparationSeconds = (float)ring.PreparationSeconds;
        _ringOuterRadius = SpatialScale3D.Distance(ring.Radius);
        _ringInnerRadius = Mathf.Max(1.0f, _ringOuterRadius * 0.5f);
        _barrageDamage = _spawnContextApplied && _scaledBarrageDamage > 0
            ? _scaledBarrageDamage
            : barrage.Damage;
        _barragePreparationSeconds = (float)barrage.PreparationSeconds;
        _barrageRange = Mathf.Max(8.0f, SpatialScale3D.Distance(barrage.Range));
        _barrageLength = Mathf.Max(6.0f, _barrageRange);
        _health.SetMaxHealth(_spawnContextApplied ? AppliedMaxHealth : definition.MaxHealth);
        _health.SetDefensiveStats(new Stats { FireResistance = definition.FireResistance });
    }
}
