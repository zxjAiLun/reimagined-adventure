using System;
using Arpg.Domain;
using Godot;

public enum SpitterState
{
    Approaching,
    HoldingRange,
    Retreating,
    Dead,
}

public partial class SpitterController : CharacterBody2D, IDamageable
{
    [Export] public float MoveSpeed { get; set; } = 72.0f;
    [Export] public float Radius { get; set; } = 16.0f;
    [Export] public float PreferredRange { get; set; } = 260.0f;
    [Export] public float RangeTolerance { get; set; } = 45.0f;
    [Export] public float AttackRange { get; set; } = 420.0f;
    [Export] public int ProjectileDamage { get; set; } = 6;
    [Export] public float AttackCooldown { get; set; } = 1.6f;
    [Export] public PackedScene ProjectileScene { get; set; }

    public int CurrentHealth => _health?.CurrentHealth ?? 0;
    public int MaxHealth => _health?.MaxHealth ?? 0;
    public bool IsAlive => _health?.IsAlive ?? false;
    public SpitterState State { get; private set; } = SpitterState.Approaching;
    public int ProjectilesFired { get; private set; }

    private HealthComponent _health;
    private PlayerController _player;
    private float _attackCooldownRemaining;
    private bool _deathHandled;
    private Label _healthLabel;

    public override void _Ready()
    {
        AddToGroup("damageables");
        AddToGroup("enemies");
        AddToGroup("spitter_enemies");

        _health = GetNode<HealthComponent>("HealthComponent");
        _health.Died += OnDied;
        _healthLabel = GetNodeOrNull<Label>("HealthLabel");
        FindPlayer();
        RefreshVisuals();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsAlive || State == SpitterState.Dead)
        {
            return;
        }

        _attackCooldownRemaining = Mathf.Max(
            0.0f,
            _attackCooldownRemaining - (float)delta);

        if (_player == null || !_player.IsAlive)
        {
            FindPlayer();
            Velocity = Vector2.Zero;
            return;
        }

        var toPlayer = _player.GlobalPosition - GlobalPosition;
        var distance = toPlayer.Length();
        var lowerRange = Mathf.Max(0.0f, PreferredRange - RangeTolerance);
        var upperRange = PreferredRange + RangeTolerance;

        if (distance > AttackRange)
        {
            State = SpitterState.Approaching;
            MoveInDirection(toPlayer);
        }
        else if (distance < lowerRange && distance > 0.001f)
        {
            State = SpitterState.Retreating;
            MoveInDirection(-toPlayer);
        }
        else if (distance >= lowerRange && distance <= upperRange)
        {
            State = SpitterState.HoldingRange;
            Velocity = Vector2.Zero;
            if (_attackCooldownRemaining <= 0.0f)
            {
                FireAtPlayer();
            }
        }
        else
        {
            State = SpitterState.Approaching;
            MoveInDirection(toPlayer);
        }

        QueueRedraw();
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

    public override void _Draw()
    {
        var bodyColor = State == SpitterState.Dead
            ? new Color(0.20f, 0.25f, 0.22f, 1.0f)
            : new Color(0.32f, 0.76f, 0.40f, 1.0f);
        DrawCircle(Vector2.Zero, Radius, bodyColor);
        DrawArc(Vector2.Zero, Radius, 0.0f, Mathf.Tau, 24, new Color(0.74f, 1.0f, 0.72f, 1.0f), 2.0f);

        var direction = _player != null
            ? (_player.GlobalPosition - GlobalPosition).Normalized()
            : Vector2.Left;
        DrawLine(Vector2.Zero, direction * (Radius + 12.0f), new Color(0.82f, 1.0f, 0.76f, 0.9f), 3.0f);

        var healthRatio = MaxHealth > 0 ? (float)CurrentHealth / MaxHealth : 0.0f;
        DrawRect(new Rect2(-Radius, Radius + 8.0f, Radius * 2.0f, 5.0f), new Color(0.10f, 0.10f, 0.14f, 1.0f));
        DrawRect(new Rect2(-Radius, Radius + 8.0f, Radius * 2.0f * healthRatio, 5.0f), new Color(0.34f, 0.92f, 0.40f, 1.0f));
    }

    private void FindPlayer()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
    }

    private void MoveInDirection(Vector2 direction)
    {
        if (direction.LengthSquared() <= 0.001f)
        {
            Velocity = Vector2.Zero;
            return;
        }

        Velocity = direction.Normalized() * MoveSpeed;
        MoveAndSlide();
    }

    private void FireAtPlayer()
    {
        if (_player == null || ProjectileScene == null || !_player.IsAlive)
        {
            return;
        }

        var direction = (_player.GlobalPosition - GlobalPosition).Normalized();
        var projectile = ProjectileScene.Instantiate<BasicProjectile>();
        GetParent().AddChild(projectile);
        projectile.GlobalPosition = GlobalPosition + direction * (Radius + 8.0f);
        projectile.Launch(
            direction,
            new DamageRequest(ProjectileDamage, DamageType.Poison, "spitter_acid"));
        ProjectilesFired++;
        _attackCooldownRemaining = Mathf.Max(0.05f, AttackCooldown);
    }

    private void OnDied()
    {
        if (_deathHandled)
        {
            return;
        }

        _deathHandled = true;
        State = SpitterState.Dead;
        Velocity = Vector2.Zero;
        CollisionLayer = 0;
        CollisionMask = 0;
        SetPhysicsProcess(false);
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        if (_healthLabel != null)
        {
            _healthLabel.Text = $"SPITTER {CurrentHealth}/{MaxHealth}\n{State}";
        }

        QueueRedraw();
    }
}
