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

public partial class FeralController3D : CharacterBody3D, ICombatTarget
{
    [Export] public float MoveSpeed { get; set; } = 2.4f;
    [Export] public float AttackRange { get; set; } = 1.25f;
    [Export] public int ContactDamage { get; set; } = 8;
    [Export] public float AttackCooldown { get; set; } = 1.0f;
    [Export] public float AttackWindupSeconds { get; set; } = 0.35f;
    [Export] public float AttackRecoverySeconds { get; set; } = 0.40f;
    [Export] public PackedScene TelegraphScene { get; set; }
    [Export] public PackedScene ItemDropScene { get; set; }

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
    public bool NavigationMovementSuppressed { get; set; }

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
    private PlayerController3D _player;
    private Label3D _healthLabel;
    private AreaTelegraph3D _activeTelegraph;
    private float _attackCooldownRemaining;
    private float _stateRemaining;
    private bool _deathHandled;
    private RunSessionNode _runSession;
    private EnemyNavigation3D _navigation;
    private EnemyCrowdAgent3D _crowdAgent;

    public override void _Ready()
    {
        AddToGroup("damageables_3d");
        AddToGroup("enemies_3d");
        _health = GetNode<HealthComponent>("HealthComponent");
        _damageFeedback = GetNodeOrNull<DamageFeedbackSource3D>("DamageFeedbackSource3D");
        _hitFlash = GetNodeOrNull<HitFlash3D>("HitFlash3D");
        _deathFeedback = GetNodeOrNull<DeathFeedback3D>("DeathFeedback3D");
        _health.Died += OnDied;
        _healthLabel = GetNodeOrNull<Label3D>("HealthLabel");
        _navigation = GetNodeOrNull<EnemyNavigation3D>("EnemyNavigation3D");
        _crowdAgent = GetNodeOrNull<EnemyCrowdAgent3D>("EnemyCrowdAgent3D");
        _runSession = MapRuntimeScope3D.FindRunSession(this);
        FindPlayer();
        RefreshVisuals();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsAlive || State == FeralState3D.Dead)
        {
            return;
        }

        var frameDelta = (float)delta;
        _attackCooldownRemaining = Mathf.Max(0.0f, _attackCooldownRemaining - frameDelta);
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
                _stateRemaining -= frameDelta;
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
                _stateRemaining -= frameDelta;
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

        var result = _health.ApplyDamage(request);
        if (result.DamageApplied > 0)
        {
            _damageFeedback?.Publish(result);
            _hitFlash?.Trigger();
        }

        RefreshVisuals();
        return result;
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
            var result = _player.ApplyDamage(new DamageRequest(
                ContactDamage,
                DamageType.Physical,
                "feral_contact_3d",
                CombatFaction.Enemy));
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
            GD.PushError("Feral3D cannot drop loot without a RunSessionNode.");
            return;
        }

        var drop = ItemDropScene.Instantiate<ItemDrop3D>();
        GetParent().AddChild(drop);
        drop.GlobalPosition = GlobalPosition;
        drop.Configure(_runSession.GenerateWeaponDrop(_runSession.CurrentMapLevel));
    }

    private void RefreshVisuals()
    {
        if (_healthLabel != null)
        {
            _healthLabel.Text = $"FERAL 3D {CurrentHealth}/{MaxHealth}\n{State}";
        }
    }
}
