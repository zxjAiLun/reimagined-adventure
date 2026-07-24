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
public partial class SpitterController3D : CharacterBody3D, ICombatTarget
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

    private HealthComponent _health;
    private PlayerController3D _player;
    private RunSessionNode _runSession;
    private Label3D _healthLabel;
    private LineTelegraph3D _activeTelegraph;
    private float _attackCooldownRemaining;
    private float _stateRemaining;
    private bool _deathHandled;
    private bool _launchPerformed;

    public override void _Ready()
    {
        AddToGroup("damageables_3d");
        AddToGroup("enemies_3d");
        AddToGroup("spitters_3d");
        _health = GetNode<HealthComponent>("HealthComponent");
        _health.Died += OnDied;
        _healthLabel = GetNodeOrNull<Label3D>("HealthLabel");
        _runSession = GetTree().GetFirstNodeInGroup("run_sessions") as RunSessionNode;
        FindPlayer();
        RefreshVisuals();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsAlive || State == SpitterState3D.Dead)
        {
            return;
        }

        var frameDelta = (float)delta;
        _attackCooldownRemaining = Mathf.Max(0.0f, _attackCooldownRemaining - frameDelta);
        if (_player == null || !GodotObject.IsInstanceValid(_player))
        {
            _player = GetTree().GetFirstNodeInGroup("player_3d") as PlayerController3D;
        }

        if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsAlive)
        {
            CancelAttack();
            Velocity = Vector3.Zero;
            RefreshVisuals();
            return;
        }

        switch (State)
        {
            case SpitterState3D.Aim:
                Velocity = Vector3.Zero;
                _stateRemaining -= frameDelta;
                if (_stateRemaining <= 0.0f)
                {
                    BeginWindup();
                }

                break;
            case SpitterState3D.Windup:
                Velocity = Vector3.Zero;
                _stateRemaining -= frameDelta;
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
                _stateRemaining -= frameDelta;
                if (_stateRemaining <= 0.0f)
                {
                    State = SpitterState3D.HoldingRange;
                    _attackCooldownRemaining = Mathf.Max(0.05f, AttackCooldown);
                }

                break;
            case SpitterState3D.Approaching:
            case SpitterState3D.HoldingRange:
            case SpitterState3D.Retreating:
                MoveOrBeginAim();
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

        var result = _health.ApplyDamage(request);
        RefreshVisuals();
        return result;
    }

    private void FindPlayer()
    {
        _player = GetTree().GetFirstNodeInGroup("player_3d") as PlayerController3D;
    }

    private void MoveOrBeginAim()
    {
        var toPlayer = _player.GlobalPosition - GlobalPosition;
        toPlayer.Y = 0.0f;
        var distance = toPlayer.Length();
        if (distance < MinimumRange)
        {
            State = SpitterState3D.Retreating;
            Velocity = distance > 0.001f
                ? -toPlayer.Normalized() * MoveSpeed
                : Vector3.Back;
            MoveAndSlide();
        }
        else if (distance > PreferredRange)
        {
            State = SpitterState3D.Approaching;
            Velocity = toPlayer.LengthSquared() > 0.001f
                ? toPlayer.Normalized() * MoveSpeed
                : Vector3.Zero;
            MoveAndSlide();
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
        projectile.Launch(
            LockedDirection,
            new DamageRequest(
                ProjectileDamage,
                DamageType.Poison,
                "spitter_acid_3d",
                CombatFaction.Enemy));
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
            GD.PushError("Spitter3D cannot drop loot without a RunSessionNode.");
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
            _healthLabel.Text = $"SPITTER 3D {CurrentHealth}/{MaxHealth}\n{State}";
        }
    }
}
