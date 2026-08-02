using System;
using Arpg.Domain;
using Godot;

public enum FeralState3D
{
    Chasing,
    Windup,
    Impact,
    Recovery,
    Dead,
}

public partial class FeralController3D : CharacterBody3D, ICombatTarget, IEnemySpawnConfigurable3D, IEliteRuntime3D
{
    [Export] public float MoveSpeed { get; set; } = 2.4f;
    [Export] public float AttackRange { get; set; } = 1.25f;
    [Export] public int ContactDamage { get; set; } = 8;
    [Export] public float AttackCooldown { get; set; } = 1.0f;
    [Export] public float AttackWindupSeconds { get; set; } = 0.35f;
    [Export] public float AttackRecoverySeconds { get; set; } = 0.40f;
    [Export] public PackedScene TelegraphScene { get; set; }
    [Export] public PackedScene ItemDropScene { get; set; }

    /// <summary>
    /// Compatibility hook for the deterministic scaling smoke. Production
    /// deaths use LootDropProfiles.Feral below.
    /// </summary>
    public bool ForceGuaranteedDropForTest { get; set; }

    public int CurrentHealth => _health?.CurrentHealth ?? 0;
    public int MaxHealth => _health?.MaxHealth ?? 0;
    public bool IsAlive => _health?.IsAlive ?? false;
    public CombatFaction Faction => CombatFaction.Enemy;
    public FeralState3D State { get; private set; } = FeralState3D.Chasing;
    public int ContactAttackCount { get; private set; }
    public int ImpactCount { get; private set; }
    public int SuccessfulContactAttackCount { get; private set; }
    public Vector3 LockedTargetPosition { get; private set; }
    public AreaTelegraph3D ActiveTelegraph => _activeTelegraph;
    public EnemyNavigation3D Navigation => _navigation;
    public EnemyCrowdAgent3D CrowdAgent => _crowdAgent;
    public RunSessionNode RunSession => _runSession;
    public PlayerController3D TargetPlayer => _player;
    public AilmentComponent3D Ailments => _ailments;
    public bool NavigationMovementSuppressed { get; set; }
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
    public EliteModifierDefinition EliteModifier { get; private set; }
    public string EliteModifierId => EliteModifier?.Id ?? string.Empty;
    public ulong EliteSelectionSeed { get; private set; }
    public int EliteAppliedCount { get; private set; }
    public VolcanicDeathEffect3D ActiveVolcanicDeathEffect { get; private set; }
    public bool IsBossAdd { get; private set; }

    public void ResetForNavigationPressureTest(Vector3 position)
    {
        if (!IsAlive)
        {
            return;
        }

        _activeTelegraph?.Cancel();
        _activeTelegraph = null;
        State = FeralState3D.Chasing;
        _stateRemaining = 0.0f;
        _attackCooldownRemaining = 0.0f;
        GlobalPosition = new Vector3(position.X, 0.0f, position.Z);
        _navigation?.RequestRepath();
    }

    private HealthComponent _health;
    private DamageFeedbackSource3D _damageFeedback;
    private HitFlash3D _hitFlash;
    private DeathFeedback3D _deathFeedback;
    private AilmentComponent3D _ailments;
    private PlayerController3D _player;
    private Label3D _healthLabel;
    private AreaTelegraph3D _activeTelegraph;
    private float _attackCooldownRemaining;
    private float _stateRemaining;
    private bool _deathHandled;
    private RunSessionNode _runSession;
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
            throw new InvalidOperationException("Feral spawn context was already applied.");
        }

        context.Validate();
        if (context.IsBoss)
        {
            throw new InvalidOperationException("Feral cannot use a boss spawn context.");
        }

        var health = GetNodeOrNull<HealthComponent>("HealthComponent")
            ?? throw new InvalidOperationException("Feral is missing HealthComponent.");
        var baseHealth = health.MaxHealth;
        var baseDamage = ContactDamage;
        var scaledHealth = MapScaling.EnemyHp(baseHealth, context.MapLevel, context.MapModifier);
        var scaledDamage = MapScaling.EnemyDamage(baseDamage, context.MapLevel, context.MapModifier);
        if (context.EliteModifier != null)
        {
            scaledHealth = EliteRuntime3D.ScaleInt(scaledHealth, context.EliteModifier.HealthMultiplier);
            scaledDamage = EliteRuntime3D.ScaleInt(scaledDamage, context.EliteModifier.DamageMultiplier);
            MoveSpeed *= (float)context.EliteModifier.MoveSpeedMultiplier;
            AttackWindupSeconds /= (float)context.EliteModifier.ActionSpeedMultiplier;
            AttackRecoverySeconds /= (float)context.EliteModifier.ActionSpeedMultiplier;
            health.Armor += context.EliteModifier.ArmorBonus;
            EliteModifier = context.EliteModifier;
            EliteSelectionSeed = context.EliteSelectionSeed;
            EliteAppliedCount++;
        }
        health.SetMaxHealth(scaledHealth);
        ContactDamage = scaledDamage;

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
        AppliedPrimaryDamage = scaledDamage;
        AppliedSecondaryDamage = 0;
        AppliedDropItemLevel = context.DropItemLevel;
        SpawnEncounterId = context.EncounterId;
        SpawnWaveId = context.WaveId;
        SpawnOrdinal = context.SpawnOrdinal;
        IsBossAdd = context.IsBossAdd;
    }

    public override void _Ready()
    {
        AddToGroup("damageables_3d");
        AddToGroup("enemies_3d");
        _health = GetNode<HealthComponent>("HealthComponent");
        _damageFeedback = GetNodeOrNull<DamageFeedbackSource3D>("DamageFeedbackSource3D");
        _hitFlash = GetNodeOrNull<HitFlash3D>("HitFlash3D");
        _deathFeedback = GetNodeOrNull<DeathFeedback3D>("DeathFeedback3D");
        _ailments = GetNodeOrNull<AilmentComponent3D>("AilmentComponent3D");
        _health.Died += OnDied;
        _health.DamageTaken += OnDamageTaken;
        _healthLabel = GetNodeOrNull<Label3D>("HealthLabel");
        _navigation = GetNodeOrNull<EnemyNavigation3D>("EnemyNavigation3D");
        _crowdAgent = GetNodeOrNull<EnemyCrowdAgent3D>("EnemyCrowdAgent3D");
        if (!_spawnContextApplied)
        {
            AppliedMaxHealth = _health.MaxHealth;
            AppliedPrimaryDamage = ContactDamage;
            AppliedSecondaryDamage = 0;
            SpawnOrdinal = 0;
        }

        _runSession = _spawnContext?.RunSession ?? MapRuntimeScope3D.FindRunSession(this);
        _player = _spawnContext?.Player;
        _player ??= MapRuntimeScope3D.FindPlayer(this);
        RefreshVisuals();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsAlive || State == FeralState3D.Dead)
        {
            return;
        }

        var frameDelta = (float)delta;
        var actionSpeedMultiplier = (float)(_ailments?.ActionSpeedMultiplier ?? 1.0);
        _attackCooldownRemaining = Mathf.Max(
            0.0f,
            _attackCooldownRemaining - frameDelta * actionSpeedMultiplier);
        if (_player == null || !GodotObject.IsInstanceValid(_player))
        {
            FindPlayer();
        }

        if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsAlive)
        {
            CancelAttack();
            Velocity = Vector3.Zero;
            _navigation?.Stop();
            State = FeralState3D.Chasing;
            RefreshVisuals();
            return;
        }

        switch (State)
        {
            case FeralState3D.Windup:
                Velocity = Vector3.Zero;
                _stateRemaining -= frameDelta * actionSpeedMultiplier;
                _activeTelegraph?.SetProgress(
                    1.0f - _stateRemaining / Mathf.Max(0.01f, AttackWindupSeconds));
                if (_stateRemaining <= 0.0f)
                {
                    BeginImpact();
                }

                break;
            case FeralState3D.Impact:
                Velocity = Vector3.Zero;
                State = FeralState3D.Recovery;
                _stateRemaining = Mathf.Max(0.01f, AttackRecoverySeconds);
                break;
            case FeralState3D.Recovery:
                Velocity = Vector3.Zero;
                _stateRemaining -= frameDelta * actionSpeedMultiplier;
                if (_stateRemaining <= 0.0f)
                {
                    State = FeralState3D.Chasing;
                    _attackCooldownRemaining = Mathf.Max(0.05f, AttackCooldown);
                }

                break;
            case FeralState3D.Chasing:
                ChaseOrBeginAttack(frameDelta);
                break;
            case FeralState3D.Dead:
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

    private void ChaseOrBeginAttack(float frameDelta)
    {
        var toPlayer = _player.GlobalPosition - GlobalPosition;
        toPlayer.Y = 0.0f;
        var distance = toPlayer.Length();
        if (distance <= AttackRange)
        {
            Velocity = Vector3.Zero;
            if (_attackCooldownRemaining <= 0.0f)
            {
                BeginWindup();
            }

            return;
        }

        if (_navigation == null)
        {
            Velocity = Vector3.Zero;
            return;
        }

        var previousPosition = GlobalPosition;
        _navigation.SetTarget(_player.GlobalPosition);
        var direction = _navigation.GetDesiredDirection(GlobalPosition, frameDelta);
        var navigationVelocity = direction.LengthSquared() > 0.001f
            ? direction * MoveSpeed
            : Vector3.Zero;
        Velocity = _crowdAgent?.CombineNavigationVelocity(navigationVelocity, MoveSpeed)
            ?? navigationVelocity;
        if (direction.LengthSquared() > 0.001f && !NavigationMovementSuppressed)
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

    private void FindPlayer()
    {
        _player = MapRuntimeScope3D.FindPlayer(this);
    }

    private void BeginWindup()
    {
        State = FeralState3D.Windup;
        _stateRemaining = Mathf.Max(0.01f, AttackWindupSeconds);
        LockedTargetPosition = _player.GlobalPosition;
        _activeTelegraph = CreateAreaTelegraph(AttackRange, GlobalPosition, AttackWindupSeconds);
    }

    private void BeginImpact()
    {
        State = FeralState3D.Impact;
        ImpactCount++;
        ContactAttackCount++;
        _activeTelegraph?.Complete();
        _activeTelegraph = null;

        var toPlayer = _player.GlobalPosition - GlobalPosition;
        toPlayer.Y = 0.0f;
        if (_player.IsAlive && toPlayer.Length() <= AttackRange)
        {
            var request = EliteModifier == null
                ? new DamageRequest(
                    ContactDamage,
                    DamageType.Physical,
                    "feral_contact_3d",
                    CombatFaction.Enemy)
                : EliteRuntime3D.BuildEnemyAttack(
                    EliteModifier,
                    ContactDamage,
                    DamageType.Physical,
                    "feral_contact_3d");
            var result = _player.ApplyDamage(request);
            if (result.DamageApplied > 0)
            {
                SuccessfulContactAttackCount++;
            }
        }
    }

    private AreaTelegraph3D CreateAreaTelegraph(float radius, Vector3 position, float duration)
    {
        if (TelegraphScene == null || GetParent() == null)
        {
            return null;
        }

        var telegraph = TelegraphScene.Instantiate<AreaTelegraph3D>();
        GetParent().AddChild(telegraph);
        telegraph.Activate(radius, position, duration);
        return telegraph;
    }

    private void CancelAttack()
    {
        _activeTelegraph?.Cancel();
        _activeTelegraph = null;
        if (State == FeralState3D.Windup
            || State == FeralState3D.Impact
            || State == FeralState3D.Recovery)
        {
            State = FeralState3D.Chasing;
        }
    }

    private void OnDied()
    {
        if (_deathHandled)
        {
            return;
        }

        _deathHandled = true;
        _activeTelegraph?.Cancel();
        _activeTelegraph = null;
        State = FeralState3D.Dead;
        _crowdAgent?.SetActive(false);
        Velocity = Vector3.Zero;
        CollisionLayer = 0;
        CollisionMask = 0;
        SetPhysicsProcess(false);
        _deathFeedback?.Play();
        AwardExperienceIfEligible();
        SpawnDrop();
        SpawnVolcanicDeathEffect();
        RefreshVisuals();
    }

    private void SpawnVolcanicDeathEffect()
    {
        var deathEffect = EliteModifier?.DeathEffect;
        var map = GetParent()?.GetParent() as Node3D;
        if (deathEffect == null || map == null)
        {
            return;
        }

        ActiveVolcanicDeathEffect = new VolcanicDeathEffect3D();
        map.AddChild(ActiveVolcanicDeathEffect);
        ActiveVolcanicDeathEffect.Configure(
            GlobalPosition,
            deathEffect,
            EliteRuntime3D.ScaleInt(AppliedPrimaryDamage, deathEffect.DamageMultiplier));
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
            ? $"feral:{GetPath()}"
            : $"feral:{SpawnEncounterId}:{SpawnWaveId}:{SpawnOrdinal}";
        ExperienceAwarded = EliteModifier == null
            ? _runSession.TryAwardExperience(ExperienceSourceKind.Feral, sourceId)
            : _runSession.TryAwardEliteExperience(ExperienceSourceKind.Feral, sourceId);
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
            GD.PushError("Feral3D cannot drop loot without a RunSessionNode.");
            return;
        }

        if (ForceGuaranteedDropForTest)
        {
            SpawnDropItem(_runSession.GenerateWeaponDrop(AppliedDropItemLevel));
            return;
        }

        if (IsBossAdd)
        {
            return;
        }

        var isElite = EliteModifier != null;
        var result = _runSession.GenerateDrops(
            new ItemRollContext(
                AppliedDropItemLevel,
                isElite ? LootSourceKind.Elite : LootSourceKind.Feral,
                RarityMultiplier: isElite ? 1.5 : 1.0),
            isElite ? LootDropProfiles.Elite : LootDropProfiles.Feral);
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
            var eliteTag = EliteModifier == null ? string.Empty : $"\n{EliteRuntime3D.DisplayTag(EliteModifier)}";
            _healthLabel.Text = $"FERAL 3D {CurrentHealth}/{MaxHealth}\n{State}{eliteTag}";
        }
    }
}
