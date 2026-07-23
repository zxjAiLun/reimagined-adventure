using System;
using Arpg.Domain;
using Godot;

public partial class PlayerController : CharacterBody2D, IDamageable
{
    [Export] public float MoveSpeed { get; set; } = 300.0f;
    [Export] public float Radius { get; set; } = 20.0f;
    [Export] public Rect2 MovementBounds { get; set; } = new(0.0f, 0.0f, 1600.0f, 900.0f);
    [Export] public PackedScene ProjectileScene { get; set; }

    public Stats EffectiveStats { get; private set; } = Stats.Neutral;
    public InventoryController Inventory { get; private set; }
    public int CurrentHealth => _health?.CurrentHealth ?? 0;
    public int MaxHealth => _health?.MaxHealth ?? 0;
    public bool IsAlive => _health?.IsAlive ?? false;
    private Vector2 _aimDirection = Vector2.Right;

    public Vector2 AimDirection => _aimDirection;

    private HealthComponent _health;

    public void SetEffectiveStats(Stats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        stats.Validate();
        EffectiveStats = stats;
    }

    public override void _Ready()
    {
        AddToGroup("player");
        _health = GetNode<HealthComponent>("HealthComponent");
        Inventory = GetNodeOrNull<InventoryController>("InventoryController");
        _health.Died += OnDied;
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        var movement = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        if (movement.LengthSquared() > 1.0f)
        {
            movement = movement.Normalized();
        }

        Velocity = movement * MoveSpeed * (float)EffectiveStats.MoveSpeedMultiplier;
        MoveAndSlide();
        ClampToMovementBounds();

        UpdateAimDirection();
        QueueRedraw();
    }

    public override void _Draw()
    {
        var bodyColor = IsAlive
            ? new Color(0.20f, 0.75f, 1.0f, 1.0f)
            : new Color(0.25f, 0.28f, 0.34f, 1.0f);
        DrawCircle(Vector2.Zero, Radius, bodyColor);
        DrawArc(Vector2.Zero, Radius, 0.0f, Mathf.Tau, 32, new Color(0.85f, 0.95f, 1.0f, 1.0f), 2.0f);
        DrawLine(Vector2.Zero, Vector2.Right * (Radius + 12.0f), new Color(0.95f, 0.98f, 1.0f, 0.9f), 4.0f);
    }

    public DamageResult ApplyDamage(DamageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_health == null || !IsAlive)
        {
            return new DamageResult(0, false);
        }

        var result = _health.ApplyDamage(request);
        QueueRedraw();
        return result;
    }

    public bool CastSpreadShot(SkillDefinition definition)
    {
        if (!IsAlive || ProjectileScene == null || definition.CastType != SkillCastType.Projectile)
        {
            return false;
        }

        var damage = CombatMath.SkillDamage(
            definition.BaseDamage,
            EffectiveStats,
            definition.DamageType,
            SkillDamageCategory.Projectile);
        var projectileCount = Mathf.Max(1, definition.ProjectileCount + EffectiveStats.ProjectileCountBonus);
        var halfSpread = Mathf.DegToRad((float)definition.SpreadAngleDegrees * 0.5f);
        var step = projectileCount > 1
            ? Mathf.DegToRad((float)definition.SpreadAngleDegrees) / (projectileCount - 1)
            : 0.0f;

        for (var index = 0; index < projectileCount; index++)
        {
            var angle = projectileCount > 1
                ? -halfSpread + step * index
                : 0.0f;
            var direction = _aimDirection.Rotated(angle).Normalized();
            var projectile = ProjectileScene.Instantiate<BasicProjectile>();
            GetParent().AddChild(projectile);
            projectile.GlobalPosition = GlobalPosition + direction * (Radius + 8.0f);
            projectile.Launch(
                direction,
                new DamageRequest(damage, definition.DamageType, definition.Id));
        }

        return true;
    }

    public bool CastAreaSkill(SkillDefinition definition, Vector2 targetPosition, PackedScene areaEffectScene)
    {
        if (!IsAlive || areaEffectScene == null || !definition.IsArea)
        {
            return false;
        }

        var damage = CombatMath.SkillDamage(
            definition.BaseDamage,
            EffectiveStats,
            definition.DamageType,
            SkillDamageCategory.Area);
        var effect = areaEffectScene.Instantiate<SkillAreaEffect>();
        effect.Configure(
            definition,
            targetPosition,
            new DamageRequest(damage, definition.DamageType, definition.Id));
        GetParent().AddChild(effect);
        return true;
    }

    public void PerformDash(double distance)
    {
        if (!IsAlive || distance <= 0.0)
        {
            return;
        }

        GlobalPosition += _aimDirection * (float)distance;
        ClampToMovementBounds();
        QueueRedraw();
    }

    private void UpdateAimDirection()
    {
        var mouseOffset = GetGlobalMousePosition() - GlobalPosition;
        if (mouseOffset.LengthSquared() > 0.001f)
        {
            _aimDirection = mouseOffset.Normalized();
        }

        Rotation = _aimDirection.Angle();
    }

    private void ClampToMovementBounds()
    {
        var left = MovementBounds.Position.X + Radius;
        var right = MovementBounds.End.X - Radius;
        var top = MovementBounds.Position.Y + Radius;
        var bottom = MovementBounds.End.Y - Radius;

        GlobalPosition = new Vector2(
            Mathf.Clamp(GlobalPosition.X, left, right),
            Mathf.Clamp(GlobalPosition.Y, top, bottom));
    }

    private void OnDied()
    {
        Velocity = Vector2.Zero;
        SetPhysicsProcess(false);
        QueueRedraw();
    }
}
