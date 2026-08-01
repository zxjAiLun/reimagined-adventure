using System;
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
    [Export] public PackedScene ItemDropScene { get; set; }

    public CombatFaction Faction => CombatFaction.Enemy;
    public int CurrentHealth => _health?.CurrentHealth ?? 0;
    public int MaxHealth => _health?.MaxHealth ?? 0;
    public bool IsAlive => _health?.IsAlive ?? false;
    public BrimstoneColossusState3D State { get; private set; } = BrimstoneColossusState3D.Idle;
    public int MagmaSlamCount { get; private set; }
    public int MagmaSlamImpactCount { get; private set; }
    public int FlameSpearCount { get; private set; }
    public int FlameSpearLaunchCount { get; private set; }
    public float SlamRadius => _slamRadius;
    public EnemyNavigation3D Navigation => _navigation;
    public EnemyCrowdAgent3D CrowdAgent => _crowdAgent;
    public RunSessionNode RunSession => _runSession;
    public PlayerController3D TargetPlayer => _player;
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
    public int AppliedMapLevel { get; private set; } = 1;
    public int AppliedMaxHealth { get; private set; }
    public int AppliedPrimaryDamage { get; private set; }
    public int AppliedSecondaryDamage { get; private set; }
    public int AppliedDropItemLevel { get; private set; } = 1;
    public string SpawnEncounterId { get; private set; } = string.Empty;
    public string SpawnWaveId { get; private set; } = string.Empty;
    public int SpawnOrdinal { get; private set; }
    public int SpawnContextAppliedCount { get; private set; }

    private HealthComponent _health;
    private DamageFeedbackSource3D _damageFeedback;
    private HitFlash3D _hitFlash;
    private DeathFeedback3D _deathFeedback;
    private PlayerController3D _player;
    private RunSessionNode _runSession;
    private Label3D _healthLabel;
    private AreaTelegraph3D _activeSlamTelegraph;
    private LineTelegraph3D _activeSpearTelegraph;
    private float _moveSpeed;
    private float _slamDamage;
    private float _slamPreparationSeconds;
    private float _slamRadius;
    private float _slamRange;
    private float _spearDamage;
    private float _spearPreparationSeconds;
    private float _spearRange;
    private float _recoverySeconds;
    private float _stateRemaining;
    private bool _nextAttackIsSlam = true;
    private bool _slamImpactApplied;
    private bool _spearLaunchPerformed;
    private bool _deathHandled;
    private EnemyNavigation3D _navigation;
    private EnemyCrowdAgent3D _crowdAgent;
    private EnemySpawnContext3D _spawnContext;
    private bool _spawnContextApplied;

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
        _health.Died += OnDied;
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
        switch (State)
        {
            case BrimstoneColossusState3D.Idle:
            case BrimstoneColossusState3D.Chasing:
                TryChooseAttack(frameDelta);
                break;
            case BrimstoneColossusState3D.PreparingSlam:
                Velocity = Vector3.Zero;
                _stateRemaining -= frameDelta;
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
                _stateRemaining -= frameDelta;
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
            case BrimstoneColossusState3D.Recovering:
                Velocity = Vector3.Zero;
                _stateRemaining -= frameDelta;
                if (_stateRemaining <= 0.0f)
                {
                    State = BrimstoneColossusState3D.Idle;
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

        var result = _health.ApplyDamage(request);
        if (result.DamageApplied > 0)
        {
            _damageFeedback?.Publish(result);
            _hitFlash?.Trigger();
        }

        RefreshVisuals();
        return result;
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
        if (_nextAttackIsSlam && distance <= _slamRange)
        {
            BeginMagmaSlam();
            return;
        }

        if (!_nextAttackIsSlam && distance <= _spearRange)
        {
            BeginFlameSpear();
            return;
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
        if (LineTelegraphScene == null || GetParent() == null)
        {
            return null;
        }

        var telegraph = LineTelegraphScene.Instantiate<LineTelegraph3D>();
        GetParent().AddChild(telegraph);
        telegraph.Activate(GlobalPosition, direction, length, duration);
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

    private void CancelTelegraphs()
    {
        _activeSlamTelegraph?.Cancel();
        _activeSlamTelegraph = null;
        _activeSpearTelegraph?.Cancel();
        _activeSpearTelegraph = null;
    }

    private void OnDied()
    {
        if (_deathHandled)
        {
            return;
        }

        _deathHandled = true;
        CancelTelegraphs();
        State = BrimstoneColossusState3D.Dead;
        _crowdAgent?.SetActive(false);
        Velocity = Vector3.Zero;
        CollisionLayer = 0;
        CollisionMask = 0;
        SetPhysicsProcess(false);
        _deathFeedback?.Play();
        SpawnDrop();
        RefreshVisuals();
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

        var drop = ItemDropScene.Instantiate<ItemDrop3D>();
        GetParent().AddChild(drop);
        drop.GlobalPosition = GlobalPosition;
        drop.Configure(_runSession.GenerateWeaponDrop(_runSession.CurrentMapLevel, boss: true));
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
        _moveSpeed = SpatialScale3D.Distance(definition.MoveSpeed);
        _recoverySeconds = (float)definition.RecoverySeconds;
        var slam = definition.Attack(BossAttackKind.MagmaSlam);
        var spear = definition.Attack(BossAttackKind.FlameSpear);
        _slamDamage = _spawnContextApplied ? AppliedPrimaryDamage : slam.Damage;
        _slamPreparationSeconds = (float)slam.PreparationSeconds;
        _slamRadius = SpatialScale3D.Distance(slam.Radius);
        _slamRange = SpatialScale3D.Distance(slam.Range);
        _spearDamage = _spawnContextApplied ? AppliedSecondaryDamage : spear.Damage;
        _spearPreparationSeconds = (float)spear.PreparationSeconds;
        _spearRange = SpatialScale3D.Distance(spear.Range);
        _health.SetMaxHealth(_spawnContextApplied ? AppliedMaxHealth : definition.MaxHealth);
        _health.SetDefensiveStats(new Stats { FireResistance = definition.FireResistance });
    }
}
