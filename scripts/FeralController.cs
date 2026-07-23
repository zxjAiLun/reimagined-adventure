using System;
using Arpg.Domain;
using Godot;

public enum FeralState
{
    Chasing,
    Attacking,
    Dead,
}

public partial class FeralController : CharacterBody2D, IDamageable
{
    [Export] public float MoveSpeed { get; set; } = 100.0f;
    [Export] public float Radius { get; set; } = 18.0f;
    [Export] public float AttackRange { get; set; } = 48.0f;
    [Export] public int ContactDamage { get; set; } = 8;
    [Export] public float AttackCooldown { get; set; } = 1.0f;

    public int CurrentHealth => _health?.CurrentHealth ?? 0;
    public int MaxHealth => _health?.MaxHealth ?? 0;
    public bool IsAlive => _health?.IsAlive ?? false;
    public FeralState State { get; private set; } = FeralState.Chasing;
    public int ContactAttackCount { get; private set; }

    private HealthComponent _health;
    private PlayerController _player;
    private float _attackCooldownRemaining;
    private bool _deathHandled;
    private bool _mapModifierApplied;
    private Label _healthLabel;

    public override void _Ready()
    {
        AddToGroup("damageables");
        AddToGroup("enemies");
        AddToGroup("feral_enemies");

        _health = GetNode<HealthComponent>("HealthComponent");
        _health.Died += OnDied;
        _healthLabel = GetNodeOrNull<Label>("HealthLabel");
        FindPlayer();
        RefreshVisuals();
    }

    public void ApplyMapModifier(MapModifierNode modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        if (_mapModifierApplied || _health == null)
        {
            return;
        }

        _mapModifierApplied = true;
        _health.SetMaxHealth(modifier.ScaleEnemyHealth(_health.MaxHealth));
        ContactDamage = modifier.ScaleEnemyDamage(ContactDamage);
        MoveSpeed *= (float)modifier.EnemySpeedMultiplier;
        RefreshVisuals();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsAlive || State == FeralState.Dead)
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
            State = FeralState.Chasing;
            return;
        }

        var toPlayer = _player.GlobalPosition - GlobalPosition;
        var distance = toPlayer.Length();
        if (distance <= AttackRange)
        {
            Velocity = Vector2.Zero;
            State = FeralState.Attacking;
            if (_attackCooldownRemaining <= 0.0f)
            {
                AttackPlayer();
            }
        }
        else
        {
            State = FeralState.Chasing;
            Velocity = toPlayer.Normalized() * MoveSpeed;
            MoveAndSlide();
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
        var bodyColor = State == FeralState.Dead
            ? new Color(0.20f, 0.22f, 0.25f, 1.0f)
            : new Color(0.85f, 0.34f, 0.24f, 1.0f);
        DrawCircle(Vector2.Zero, Radius, bodyColor);
        DrawArc(Vector2.Zero, Radius, 0.0f, Mathf.Tau, 24, new Color(1.0f, 0.82f, 0.60f, 1.0f), 2.0f);

        var direction = _player != null
            ? (_player.GlobalPosition - GlobalPosition).Normalized()
            : Vector2.Left;
        DrawLine(Vector2.Zero, direction * (Radius + 10.0f), new Color(1.0f, 0.92f, 0.76f, 0.9f), 3.0f);

        var healthRatio = MaxHealth > 0 ? (float)CurrentHealth / MaxHealth : 0.0f;
        DrawRect(new Rect2(-Radius, Radius + 8.0f, Radius * 2.0f, 5.0f), new Color(0.10f, 0.10f, 0.14f, 1.0f));
        DrawRect(new Rect2(-Radius, Radius + 8.0f, Radius * 2.0f * healthRatio, 5.0f), new Color(0.95f, 0.28f, 0.22f, 1.0f));
    }

    private void FindPlayer()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
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
            "feral_contact"));
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
        State = FeralState.Dead;
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
            _healthLabel.Text = $"FERAL {CurrentHealth}/{MaxHealth}\n{State}";
        }

        QueueRedraw();
    }
}
