using System;
using Arpg.Domain;
using Godot;

public enum FeralState3D
{
    Chasing,
    Attacking,
    Dead,
}

public partial class FeralController3D : CharacterBody3D, ICombatTarget
{
    [Export] public float MoveSpeed { get; set; } = 2.4f;
    [Export] public float AttackRange { get; set; } = 1.25f;
    [Export] public int ContactDamage { get; set; } = 8;
    [Export] public float AttackCooldown { get; set; } = 1.0f;
    [Export] public PackedScene ItemDropScene { get; set; }

    public int CurrentHealth => _health?.CurrentHealth ?? 0;
    public int MaxHealth => _health?.MaxHealth ?? 0;
    public bool IsAlive => _health?.IsAlive ?? false;
    public CombatFaction Faction => CombatFaction.Enemy;
    public FeralState3D State { get; private set; } = FeralState3D.Chasing;
    public int ContactAttackCount { get; private set; }

    private HealthComponent _health;
    private PlayerController3D _player;
    private Label3D _healthLabel;
    private float _attackCooldownRemaining;
    private bool _deathHandled;
    private RunSessionNode _runSession;

    public override void _Ready()
    {
        AddToGroup("damageables_3d");
        AddToGroup("enemies_3d");
        _health = GetNode<HealthComponent>("HealthComponent");
        _health.Died += OnDied;
        _healthLabel = GetNodeOrNull<Label3D>("HealthLabel");
        _runSession = GetTree().GetFirstNodeInGroup("run_sessions") as RunSessionNode;
        FindPlayer();
        RefreshVisuals();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsAlive || State == FeralState3D.Dead)
        {
            return;
        }

        _attackCooldownRemaining = Mathf.Max(
            0.0f,
            _attackCooldownRemaining - (float)delta);
        _player ??= GetTree().GetFirstNodeInGroup("player_3d") as PlayerController3D;
        if (_player == null || !_player.IsAlive)
        {
            Velocity = Vector3.Zero;
            State = FeralState3D.Chasing;
            return;
        }

        var toPlayer = _player.GlobalPosition - GlobalPosition;
        toPlayer.Y = 0.0f;
        var distance = toPlayer.Length();
        if (distance <= AttackRange)
        {
            Velocity = Vector3.Zero;
            State = FeralState3D.Attacking;
            if (_attackCooldownRemaining <= 0.0f)
            {
                AttackPlayer();
            }
        }
        else
        {
            State = FeralState3D.Chasing;
            Velocity = toPlayer.Normalized() * MoveSpeed;
            MoveAndSlide();
        }

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

    private void AttackPlayer()
    {
        if (_player == null || !_player.IsAlive)
        {
            return;
        }

        _player.ApplyDamage(new DamageRequest(
            ContactDamage,
            DamageType.Physical,
            "feral_contact_3d",
            CombatFaction.Enemy));
        ContactAttackCount++;
        _attackCooldownRemaining = Mathf.Max(0.05f, AttackCooldown);
    }

    private void OnDied()
    {
        if (_deathHandled)
        {
            return;
        }

        _deathHandled = true;
        State = FeralState3D.Dead;
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
