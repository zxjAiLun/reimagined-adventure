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
public partial class BrimstoneColossusController3D : CharacterBody3D, ICombatTarget
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
    public Vector3 LastSlamTelegraphCenter { get; private set; }
    public float LastSlamTelegraphRadius { get; private set; }
    public Vector3 LastSlamImpactCenter { get; private set; }
    public float LastSlamImpactRadius { get; private set; }
    public Vector3 LockedSpearDirection { get; private set; } = Vector3.Forward;
    public Vector3 LastSpearLaunchDirection { get; private set; } = Vector3.Zero;
    public float SpearTelegraphLength { get; private set; }
    public AreaTelegraph3D ActiveSlamTelegraph => _activeSlamTelegraph;
    public LineTelegraph3D ActiveSpearTelegraph => _activeSpearTelegraph;

    private HealthComponent _health;
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

    public override void _Ready()
    {
        AddToGroup("damageables_3d");
        AddToGroup("enemies_3d");
        AddToGroup("bosses_3d");
        _health = GetNode<HealthComponent>("HealthComponent");
        _health.Died += OnDied;
        _healthLabel = GetNodeOrNull<Label3D>("HealthLabel");
        _runSession = GetTree().GetFirstNodeInGroup("run_sessions") as RunSessionNode;
        ApplyDefinition(DefinitionResource?.ToDomain() ?? BossLibrary.BrimstoneColossus());
        FindPlayer();
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
            _player = GetTree().GetFirstNodeInGroup("player_3d") as PlayerController3D;
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
                TryChooseAttack();
                break;
            case BrimstoneColossusState3D.PreparingSlam:
                Velocity = Vector3.Zero;
                _stateRemaining -= frameDelta;
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
        RefreshVisuals();
        return result;
    }

    private void FindPlayer()
    {
        _player = GetTree().GetFirstNodeInGroup("player_3d") as PlayerController3D;
    }

    private void TryChooseAttack()
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
        Velocity = toPlayer.LengthSquared() > 0.001f
            ? toPlayer.Normalized() * _moveSpeed
            : Vector3.Zero;
        MoveAndSlide();
    }

    private void BeginMagmaSlam()
    {
        State = BrimstoneColossusState3D.PreparingSlam;
        MagmaSlamCount++;
        _slamImpactApplied = false;
        _stateRemaining = Mathf.Max(0.05f, _slamPreparationSeconds);
        _nextAttackIsSlam = false;
        LastSlamTelegraphCenter = GlobalPosition;
        LastSlamTelegraphRadius = _slamRadius;
        _activeSlamTelegraph = CreateAreaTelegraph(
            _slamRadius,
            GlobalPosition,
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
        LastSlamImpactCenter = GlobalPosition;
        LastSlamImpactRadius = _slamRadius;
        if (AreaEffectScene == null || GetParent() == null)
        {
            return;
        }

        var effect = AreaEffectScene.Instantiate<SkillAreaEffect3D>();
        GetParent().AddChild(effect);
        effect.ConfigureHazard(
            GlobalPosition,
            _slamRadius,
            0.0,
            0.18,
            new DamageRequest(
                (int)_slamDamage,
                DamageType.Fire,
                "magma_slam_3d",
                CombatFaction.Enemy));
        effect.SetVisualVisible(false);
        effect.ApplyImpactForTest();
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
        telegraph.Activate(GlobalPosition + Vector3.Up * 0.08f, direction, length, duration);
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
        Velocity = Vector3.Zero;
        CollisionLayer = 0;
        CollisionMask = 0;
        SetPhysicsProcess(false);
        SpawnDrop();
        RefreshVisuals();
    }

    private void SpawnDrop()
    {
        if (ItemDropScene == null || GetParent() == null)
        {
            return;
        }

        _runSession ??= GetTree().GetFirstNodeInGroup("run_sessions") as RunSessionNode;
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
        _slamDamage = slam.Damage;
        _slamPreparationSeconds = (float)slam.PreparationSeconds;
        _slamRadius = SpatialScale3D.Distance(slam.Radius);
        _slamRange = SpatialScale3D.Distance(slam.Range);
        _spearDamage = spear.Damage;
        _spearPreparationSeconds = (float)spear.PreparationSeconds;
        _spearRange = SpatialScale3D.Distance(spear.Range);
        _health.SetMaxHealth(definition.MaxHealth);
        _health.SetDefensiveStats(new Stats { FireResistance = definition.FireResistance });
    }
}
