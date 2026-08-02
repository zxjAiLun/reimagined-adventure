using System;
using Arpg.Domain;
using Godot;

public enum SpitterState3D
{
    Approaching,
    HoldingRange,
    Retreating,
    Aim,
    Windup,
    Launch,
    Recovery,
    Dead,
}

/// <summary>
/// 3D ranged enemy adapter. Aim chooses the attack, Windup locks its target
/// direction, Launch creates one projectile, and Recovery gates the next shot.
/// </summary>
public partial class SpitterController3D : CharacterBody3D, ICombatTarget, IEnemySpawnConfigurable3D, IEliteRuntime3D
{
    [Export] public float MoveSpeed { get; set; } = 2.0f;
    [Export] public float PreferredRange { get; set; } = 6.0f;
    [Export] public float MinimumRange { get; set; } = 3.5f;
    [Export] public int ProjectileDamage { get; set; } = 4;
    [Export] public float AttackCooldown { get; set; } = 1.6f;
    [Export] public float AimSeconds { get; set; } = 0.20f;
    [Export] public float TelegraphSeconds { get; set; } = 0.35f;
    [Export] public float RecoverySeconds { get; set; } = 0.25f;
    [Export] public PackedScene ProjectileScene { get; set; }
    [Export] public PackedScene TelegraphScene { get; set; }
    [Export] public PackedScene ItemDropScene { get; set; }

    /// <summary>
    /// Compatibility hook for the deterministic scaling smoke. Production
    /// deaths use LootDropProfiles.Spitter below.
    /// </summary>
    public bool ForceGuaranteedDropForTest { get; set; }

    public CombatFaction Faction => CombatFaction.Enemy;
    public int CurrentHealth => _health?.CurrentHealth ?? 0;
    public int MaxHealth => _health?.MaxHealth ?? 0;
    public bool IsAlive => _health?.IsAlive ?? false;
    public SpitterState3D State { get; private set; } = SpitterState3D.Approaching;
    public int ProjectileShotCount { get; private set; }
    public int TelegraphCount { get; private set; }
    public Vector3 LockedTargetPosition { get; private set; }
    public Vector3 LockedDirection { get; private set; } = Vector3.Forward;
    public Vector3 LastLaunchDirection { get; private set; } = Vector3.Zero;
    public LineTelegraph3D ActiveTelegraph => _activeTelegraph;
    public EnemyNavigation3D Navigation => _navigation;
    public EnemyCrowdAgent3D CrowdAgent => _crowdAgent;
    public RunSessionNode RunSession => _runSession;
    public PlayerController3D TargetPlayer => _player;
    public AilmentComponent3D Ailments => _ailments;
    public Vector3 NavigationTargetPosition => _navigation?.TargetPosition ?? Vector3.Zero;
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

    private HealthComponent _health;
    private DamageFeedbackSource3D _damageFeedback;
    private HitFlash3D _hitFlash;
    private DeathFeedback3D _deathFeedback;
    private AilmentComponent3D _ailments;
    private PlayerController3D _player;
    private RunSessionNode _runSession;
    private Label3D _healthLabel;
    private LineTelegraph3D _activeTelegraph;
    private float _attackCooldownRemaining;
    private float _stateRemaining;
    private bool _deathHandled;
    private bool _launchPerformed;
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
            throw new InvalidOperationException("Spitter spawn context was already applied.");
        }

        context.Validate();
        if (context.IsBoss)
        {
            throw new InvalidOperationException("Spitter cannot use a boss spawn context.");
        }

        var health = GetNodeOrNull<HealthComponent>("HealthComponent")
            ?? throw new InvalidOperationException("Spitter is missing HealthComponent.");
        var baseHealth = health.MaxHealth;
        var baseDamage = ProjectileDamage;
        var scaledHealth = MapScaling.EnemyHp(baseHealth, context.MapLevel, context.MapModifier);
        var scaledDamage = MapScaling.EnemyDamage(baseDamage, context.MapLevel, context.MapModifier);
        if (context.EliteModifier != null)
        {
            scaledHealth = EliteRuntime3D.ScaleInt(scaledHealth, context.EliteModifier.HealthMultiplier);
            scaledDamage = EliteRuntime3D.ScaleInt(scaledDamage, context.EliteModifier.DamageMultiplier);
            MoveSpeed *= (float)context.EliteModifier.MoveSpeedMultiplier;
            TelegraphSeconds /= (float)context.EliteModifier.ActionSpeedMultiplier;
            RecoverySeconds /= (float)context.EliteModifier.ActionSpeedMultiplier;
            health.Armor += context.EliteModifier.ArmorBonus;
            EliteModifier = context.EliteModifier;
            EliteSelectionSeed = context.EliteSelectionSeed;
            EliteAppliedCount++;
        }
        health.SetMaxHealth(scaledHealth);
        ProjectileDamage = scaledDamage;

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
        AddToGroup("spitters_3d");
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
            AppliedPrimaryDamage = ProjectileDamage;
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
        if (!IsAlive || State == SpitterState3D.Dead)
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
            RefreshVisuals();
            return;
        }

        switch (State)
        {
            case SpitterState3D.Aim:
                Velocity = Vector3.Zero;
                if (!IsWithinDistanceBand())
                {
                    State = SpitterState3D.HoldingRange;
                    break;
                }

                _stateRemaining -= frameDelta * actionSpeedMultiplier;
                if (_stateRemaining <= 0.0f)
                {
                    BeginWindup();
                }

                break;
            case SpitterState3D.Windup:
                Velocity = Vector3.Zero;
                _stateRemaining -= frameDelta * actionSpeedMultiplier;
                _activeTelegraph?.SetProgress(
                    1.0f - _stateRemaining / Mathf.Max(0.01f, TelegraphSeconds));
                if (_stateRemaining <= 0.0f)
                {
                    BeginLaunch();
                }

                break;
            case SpitterState3D.Launch:
                Velocity = Vector3.Zero;
                State = SpitterState3D.Recovery;
                _stateRemaining = Mathf.Max(0.01f, RecoverySeconds);
                break;
            case SpitterState3D.Recovery:
                Velocity = Vector3.Zero;
                _stateRemaining -= frameDelta * actionSpeedMultiplier;
                if (_stateRemaining <= 0.0f)
                {
                    State = SpitterState3D.HoldingRange;
                    _attackCooldownRemaining = Mathf.Max(0.05f, AttackCooldown);
                }

                break;
            case SpitterState3D.Approaching:
            case SpitterState3D.HoldingRange:
            case SpitterState3D.Retreating:
                MoveOrBeginAim(frameDelta);
                break;
            case SpitterState3D.Dead:
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

    private void FindPlayer()
    {
        _player = MapRuntimeScope3D.FindPlayer(this);
    }

    private void MoveOrBeginAim(float frameDelta)
    {
        var toPlayer = _player.GlobalPosition - GlobalPosition;
        toPlayer.Y = 0.0f;
        var distance = toPlayer.Length();
        if (distance < MinimumRange)
        {
            State = SpitterState3D.Retreating;
            MoveWithNavigation(GetRetreatTarget(), frameDelta);
        }
        else if (distance > PreferredRange)
        {
            State = SpitterState3D.Approaching;
            MoveWithNavigation(_player.GlobalPosition, frameDelta);
        }
        else
        {
            State = SpitterState3D.HoldingRange;
            Velocity = Vector3.Zero;
            if (_attackCooldownRemaining <= 0.0f)
            {
                BeginAim();
            }
        }
    }

    private bool IsWithinDistanceBand()
    {
        var toPlayer = _player.GlobalPosition - GlobalPosition;
        toPlayer.Y = 0.0f;
        var distance = toPlayer.Length();
        return distance >= MinimumRange && distance <= PreferredRange;
    }

    private Vector3 GetRetreatTarget()
    {
        var toPlayer = _player.GlobalPosition - GlobalPosition;
        toPlayer.Y = 0.0f;
        var away = toPlayer.LengthSquared() > 0.001f
            ? -toPlayer.Normalized()
            : Vector3.Back;
        var desiredDistance = Mathf.Max(PreferredRange, MinimumRange + 0.5f);
        return _player.GlobalPosition + away * desiredDistance;
    }

    private void MoveWithNavigation(Vector3 targetPosition, float frameDelta)
    {
        if (_navigation == null)
        {
            Velocity = Vector3.Zero;
            return;
        }

        var previousPosition = GlobalPosition;
        _navigation.SetTarget(targetPosition);
        var direction = _navigation.GetDesiredDirection(GlobalPosition, frameDelta);
        var navigationVelocity = direction.LengthSquared() > 0.001f
            ? direction * MoveSpeed
            : Vector3.Zero;
        Velocity = _crowdAgent?.CombineNavigationVelocity(navigationVelocity, MoveSpeed)
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

    private void BeginAim()
    {
        State = SpitterState3D.Aim;
        _stateRemaining = Mathf.Max(0.01f, AimSeconds);
        _launchPerformed = false;
    }

    private void BeginWindup()
    {
        State = SpitterState3D.Windup;
        _stateRemaining = Mathf.Max(0.01f, TelegraphSeconds);
        LockedTargetPosition = _player.GlobalPosition;
        var direction = LockedTargetPosition - GlobalPosition;
        direction.Y = 0.0f;
        LockedDirection = direction.LengthSquared() > 0.001f
            ? direction.Normalized()
            : Vector3.Forward;
        TelegraphCount++;
        _activeTelegraph = CreateLineTelegraph(
            LockedDirection,
            Mathf.Max(1.0f, direction.Length() + 0.8f),
            TelegraphSeconds);
    }

    private void BeginLaunch()
    {
        State = SpitterState3D.Launch;
        _activeTelegraph?.Complete();
        _activeTelegraph = null;
        if (!_launchPerformed)
        {
            _launchPerformed = true;
            FireProjectile();
        }

        _stateRemaining = 0.0f;
    }

    private LineTelegraph3D CreateLineTelegraph(Vector3 direction, float length, float duration)
    {
        if (TelegraphScene == null || GetParent() == null)
        {
            return null;
        }

        var telegraph = TelegraphScene.Instantiate<LineTelegraph3D>();
        GetParent().AddChild(telegraph);
        telegraph.Activate(GlobalPosition, direction, length, duration);
        return telegraph;
    }

    private void FireProjectile()
    {
        if (ProjectileScene == null || !_player.IsAlive || GetParent() == null)
        {
            return;
        }

        var projectile = ProjectileScene.Instantiate<BasicProjectile3D>();
        GetParent().AddChild(projectile);
        projectile.GlobalPosition = GlobalPosition + LockedDirection * 0.8f + Vector3.Up * 0.55f;
        var request = EliteModifier == null
            ? new DamageRequest(
                ProjectileDamage,
                DamageType.Poison,
                "spitter_acid_3d",
                CombatFaction.Enemy)
            : EliteRuntime3D.BuildEnemyAttack(
                EliteModifier,
                ProjectileDamage,
                DamageType.Poison,
                "spitter_acid_3d");
        projectile.Launch(LockedDirection, request);
        LastLaunchDirection = projectile.LaunchDirection;
        ProjectileShotCount++;
    }

    private void CancelAttack()
    {
        _activeTelegraph?.Cancel();
        _activeTelegraph = null;
        if (State == SpitterState3D.Aim
            || State == SpitterState3D.Windup
            || State == SpitterState3D.Launch
            || State == SpitterState3D.Recovery)
        {
            State = SpitterState3D.HoldingRange;
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
        State = SpitterState3D.Dead;
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
            ? $"spitter:{GetPath()}"
            : $"spitter:{SpawnEncounterId}:{SpawnWaveId}:{SpawnOrdinal}";
        ExperienceAwarded = EliteModifier == null
            ? _runSession.TryAwardExperience(ExperienceSourceKind.Spitter, sourceId)
            : _runSession.TryAwardEliteExperience(ExperienceSourceKind.Spitter, sourceId);
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
            GD.PushError("Spitter3D cannot drop loot without a RunSessionNode.");
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
                isElite ? LootSourceKind.Elite : LootSourceKind.Spitter,
                RarityMultiplier: isElite ? 1.5 : 1.0),
            isElite ? LootDropProfiles.Elite : LootDropProfiles.Spitter);
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
            _healthLabel.Text = $"SPITTER 3D {CurrentHealth}/{MaxHealth}\n{State}{eliteTag}";
        }
    }
}
