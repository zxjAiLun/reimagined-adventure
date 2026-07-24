using System;
using Arpg.Domain;
using Godot;

public enum SpitterState3D
{
    Approaching,
    HoldingRange,
    Retreating,
    PreparingAttack,
    Dead,
}

/// <summary>
/// 3D ranged enemy adapter. It keeps a readable distance, telegraphs each
/// shot, and sends an enemy-faction projectile through the shared damage path.
/// </summary>
public partial class SpitterController3D : CharacterBody3D, ICombatTarget
{
    [Export] public float MoveSpeed { get; set; } = 2.0f;
    [Export] public float PreferredRange { get; set; } = 6.0f;
    [Export] public float MinimumRange { get; set; } = 3.5f;
    [Export] public int ProjectileDamage { get; set; } = 4;
    [Export] public float AttackCooldown { get; set; } = 1.6f;
    [Export] public float TelegraphSeconds { get; set; } = 0.35f;
    [Export] public PackedScene ProjectileScene { get; set; }
    [Export] public PackedScene ItemDropScene { get; set; }

    public CombatFaction Faction => CombatFaction.Enemy;
    public int CurrentHealth => _health?.CurrentHealth ?? 0;
    public int MaxHealth => _health?.MaxHealth ?? 0;
    public bool IsAlive => _health?.IsAlive ?? false;
    public SpitterState3D State { get; private set; } = SpitterState3D.Approaching;
    public int ProjectileShotCount { get; private set; }
    public int TelegraphCount { get; private set; }

    private HealthComponent _health;
    private PlayerController3D _player;
    private RunSessionNode _runSession;
    private Label3D _healthLabel;
    private MeshInstance3D _telegraph;
    private float _attackCooldownRemaining;
    private float _stateRemaining;
    private bool _deathHandled;

    public override void _Ready()
    {
        AddToGroup("damageables_3d");
        AddToGroup("enemies_3d");
        AddToGroup("spitters_3d");
        _health = GetNode<HealthComponent>("HealthComponent");
        _health.Died += OnDied;
        _healthLabel = GetNodeOrNull<Label3D>("HealthLabel");
        _telegraph = GetNodeOrNull<MeshInstance3D>("Telegraph");
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
        _player ??= GetTree().GetFirstNodeInGroup("player_3d") as PlayerController3D;
        if (_player == null || !_player.IsAlive)
        {
            Velocity = Vector3.Zero;
            return;
        }

        var toPlayer = _player.GlobalPosition - GlobalPosition;
        toPlayer.Y = 0.0f;
        var distance = toPlayer.Length();
        if (State == SpitterState3D.PreparingAttack)
        {
            Velocity = Vector3.Zero;
            _stateRemaining -= frameDelta;
            if (_stateRemaining <= 0.0f)
            {
                FireProjectile();
                State = SpitterState3D.HoldingRange;
            }

            RefreshVisuals();
            return;
        }

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
            Velocity = toPlayer.Normalized() * MoveSpeed;
            MoveAndSlide();
        }
        else
        {
            State = SpitterState3D.HoldingRange;
            Velocity = Vector3.Zero;
            if (_attackCooldownRemaining <= 0.0f)
            {
                BeginTelegraph();
            }
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

    private void BeginTelegraph()
    {
        State = SpitterState3D.PreparingAttack;
        _stateRemaining = Mathf.Max(0.05f, TelegraphSeconds);
        _attackCooldownRemaining = Mathf.Max(0.05f, AttackCooldown);
        TelegraphCount++;
    }

    private void FireProjectile()
    {
        if (_player == null || ProjectileScene == null || !_player.IsAlive)
        {
            return;
        }

        var direction = _player.GlobalPosition - GlobalPosition;
        direction.Y = 0.0f;
        if (direction.LengthSquared() <= 0.001f)
        {
            return;
        }

        var projectile = ProjectileScene.Instantiate<BasicProjectile3D>();
        GetParent().AddChild(projectile);
        projectile.GlobalPosition = GlobalPosition + direction.Normalized() * 0.8f + Vector3.Up * 0.55f;
        projectile.Launch(
            direction,
            new DamageRequest(
                ProjectileDamage,
                DamageType.Poison,
                "spitter_acid_3d",
                CombatFaction.Enemy));
        ProjectileShotCount++;
    }

    private void OnDied()
    {
        if (_deathHandled)
        {
            return;
        }

        _deathHandled = true;
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

        if (_telegraph == null)
        {
            return;
        }

        var visible = State == SpitterState3D.PreparingAttack && _player != null && _player.IsAlive;
        _telegraph.Visible = visible;
        if (!visible)
        {
            return;
        }

        var direction = _player.GlobalPosition - GlobalPosition;
        direction.Y = 0.0f;
        var distance = direction.Length();
        if (distance <= 0.001f)
        {
            return;
        }

        _telegraph.GlobalPosition = GlobalPosition + Vector3.Up * 0.6f + direction * 0.5f;
        _telegraph.Scale = new Vector3(1.0f, 1.0f, distance);
        _telegraph.LookAt(GlobalPosition + Vector3.Up * 0.6f + direction, Vector3.Up);
    }
}
