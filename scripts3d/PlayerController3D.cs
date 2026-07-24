using System;
using System.Collections.Generic;
using Arpg.Domain;
using Godot;

/// <summary>
/// First 3D player adapter. It intentionally owns only spatial runtime work;
/// damage, skills, loot and equipment rules stay in Arpg.Domain.
/// </summary>
public partial class PlayerController3D : CharacterBody3D, ICombatTarget
{
    [Export] public float MoveSpeed { get; set; } = 5.0f;
    [Export] public Vector2 MovementBounds { get; set; } = new(11.0f, 7.0f);
    [Export] public float PickupRange { get; set; } = 2.4f;
    [Export] public PackedScene ProjectileScene { get; set; }
    [Export] public PackedScene AreaEffectScene { get; set; }

    public Stats EffectiveStats { get; private set; } = Stats.Neutral;
    public int CurrentHealth => _health?.CurrentHealth ?? 0;
    public int MaxHealth => _health?.MaxHealth ?? 0;
    public bool IsAlive => _health?.IsAlive ?? false;
    public CombatFaction Faction => CombatFaction.Player;
    public Vector3 AimDirection { get; private set; } = Vector3.Forward;
    public int SpreadShotDamage => SkillSupportMath.Damage(
        SkillLibrary.SpreadShot(),
        EffectiveStats);
    public int ItemCount => _items.Count;
    public string EquippedWeaponName => _equipment.ItemInSlot(EquipmentSlot.Weapon)?.Name ?? "none";
    public IReadOnlyList<Item> Items => _items;

    private readonly List<Item> _items = new();
    private readonly Equipment _equipment = new();
    private HealthComponent _health;
    private MouseGroundTargeting3D _targeting;
    private Stats _equipmentStats = Stats.Neutral;
    private int _baseMaxHealth;
    private float _spreadCooldown;
    private float _pulseCooldown;
    private float _dashCooldown;

    public override void _Ready()
    {
        AddToGroup("player_3d");
        AddToGroup("damageables_3d");

        _health = GetNode<HealthComponent>("HealthComponent");
        _targeting = GetNode<MouseGroundTargeting3D>("MouseGroundTargeting3D");
        _targeting.Camera = GetTree().GetFirstNodeInGroup("arena_cameras") as Camera3D;
        _baseMaxHealth = _health.MaxHealth;
        _health.Died += OnDied;
        RecalculateEffectiveStats();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsAlive)
        {
            return;
        }

        var frameDelta = (float)delta;
        _spreadCooldown = Mathf.Max(0.0f, _spreadCooldown - frameDelta);
        _pulseCooldown = Mathf.Max(0.0f, _pulseCooldown - frameDelta);
        _dashCooldown = Mathf.Max(0.0f, _dashCooldown - frameDelta);

        var movement = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        var moveDirection = new Vector3(movement.X, 0.0f, movement.Y);
        if (moveDirection.LengthSquared() > 1.0f)
        {
            moveDirection = moveDirection.Normalized();
        }

        Velocity = moveDirection * MoveSpeed * (float)EffectiveStats.MoveSpeedMultiplier;
        MoveAndSlide();
        ClampToArena();
        UpdateAimDirection();

        if (Input.IsActionPressed("skill_spread_shot"))
        {
            CastSpreadShot();
        }

        if (Input.IsActionJustPressed("skill_pulse"))
        {
            CastPulse();
        }

        if (Input.IsActionJustPressed("skill_dash"))
        {
            PerformDash(SpatialScale3D.Distance(SkillLibrary.Dash().DashDistance));
        }

        if (Input.IsActionJustPressed("pickup_item"))
        {
            TryPickupNearest();
        }

        if (Input.IsActionJustPressed("equip_item"))
        {
            TryEquipNewestWeapon();
        }
    }

    public DamageResult ApplyDamage(DamageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_health == null || !IsAlive)
        {
            return new DamageResult(0, false);
        }

        return _health.ApplyDamage(request);
    }

    public void SetAimDirectionForTest(Vector3 direction)
    {
        direction.Y = 0.0f;
        if (direction.LengthSquared() <= 0.001f)
        {
            return;
        }

        AimDirection = direction.Normalized();
        LookAt(GlobalPosition + AimDirection, Vector3.Up);
    }

    public bool CastSpreadShot()
    {
        if (!IsAlive || _spreadCooldown > 0.0f || ProjectileScene == null)
        {
            return false;
        }

        var skill = SkillLibrary.SpreadShot();
        var damage = SkillSupportMath.Damage(skill, EffectiveStats);
        var projectileCount = SkillSupportMath.ProjectileCount(skill, EffectiveStats);
        var spread = Mathf.DegToRad((float)skill.SpreadAngleDegrees);
        var halfSpread = spread * 0.5f;
        var step = projectileCount > 1 ? spread / (projectileCount - 1) : 0.0f;

        for (var index = 0; index < projectileCount; index++)
        {
            var angle = projectileCount > 1 ? -halfSpread + step * index : 0.0f;
            var direction = AimDirection.Rotated(Vector3.Up, angle).Normalized();
            var projectile = ProjectileScene.Instantiate<BasicProjectile3D>();
            GetParent().AddChild(projectile);
            projectile.GlobalPosition = GlobalPosition + direction * 0.9f + Vector3.Up * 0.65f;
            projectile.Launch(
                direction,
                new DamageRequest(
                    damage,
                    skill.DamageType,
                    skill.Id,
                    CombatFaction.Player));
        }

        _spreadCooldown = (float)SkillSupportMath.Cooldown(skill, EffectiveStats);
        return true;
    }

    public bool CastPulse()
    {
        if (!IsAlive || _pulseCooldown > 0.0f || AreaEffectScene == null)
        {
            return false;
        }

        var skill = SkillLibrary.Pulse();
        var effect = AreaEffectScene.Instantiate<SkillAreaEffect3D>();
        GetParent().AddChild(effect);
        effect.Configure(
            skill,
            GlobalPosition,
            new DamageRequest(
                SkillSupportMath.Damage(skill, EffectiveStats),
                skill.DamageType,
                skill.Id,
                CombatFaction.Player),
            Mathf.Max(1.5f, SpatialScale3D.Distance(skill.Radius)));
        _pulseCooldown = (float)SkillSupportMath.Cooldown(skill, EffectiveStats);
        return true;
    }

    public void PerformDash(double distance)
    {
        if (!IsAlive || _dashCooldown > 0.0f || distance <= 0.0)
        {
            return;
        }

        Velocity = AimDirection * (float)distance;
        MoveAndCollide(Velocity);
        ClampToArena();
        Velocity = Vector3.Zero;
        _dashCooldown = (float)SkillLibrary.Dash().CooldownSeconds;
    }

    public bool TryAddItem(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Validate();
        if (_items.Count >= 8 || _items.Exists(existing => existing.Id == item.Id))
        {
            return false;
        }

        _items.Add(item);
        return true;
    }

    public bool TryPickupNearest(float? rangeOverride = null)
    {
        var range = rangeOverride ?? PickupRange * EffectiveStats.PickupRangeMultiplier;
        ItemDrop3D nearest = null;
        var nearestDistanceSquared = range * range;
        foreach (var node in GetTree().GetNodesInGroup("item_drops_3d"))
        {
            if (node is not ItemDrop3D drop)
            {
                continue;
            }

            var distanceSquared = HorizontalDistanceSquared(drop.GlobalPosition, GlobalPosition);
            if (distanceSquared <= nearestDistanceSquared)
            {
                nearest = drop;
                nearestDistanceSquared = distanceSquared;
            }
        }

        return nearest != null && nearest.TryCollect(this);
    }

    public bool TryEquipNewestWeapon()
    {
        for (var index = _items.Count - 1; index >= 0; index--)
        {
            var item = _items[index];
            if (item.Slot != EquipmentSlot.Weapon || !_equipment.CanEquip(item, 1))
            {
                continue;
            }

            var replaced = _equipment.Equip(item, 1);
            if (replaced == null)
            {
                _items.RemoveAt(index);
            }
            else
            {
                _items[index] = replaced;
            }

            RecalculateEffectiveStats();
            return true;
        }

        return false;
    }

    public string InventorySummary()
    {
        return $"Bag {ItemCount}/8 | Weapon: {EquippedWeaponName} | Spread damage: {SpreadShotDamage}";
    }

    private void UpdateAimDirection()
    {
        if (_targeting == null || !_targeting.TryGetMouseGroundPoint(out var mouseWorld))
        {
            return;
        }

        var aim = mouseWorld - GlobalPosition;
        aim.Y = 0.0f;
        if (aim.LengthSquared() > 0.001f)
        {
            SetAimDirectionForTest(aim);
        }
    }

    private void ClampToArena()
    {
        var position = GlobalPosition;
        position.X = Mathf.Clamp(position.X, -MovementBounds.X, MovementBounds.X);
        position.Z = Mathf.Clamp(position.Z, -MovementBounds.Y, MovementBounds.Y);
        position.Y = 0.0f;
        GlobalPosition = position;
    }

    private void OnDied()
    {
        Velocity = Vector3.Zero;
        SetPhysicsProcess(false);
        CollisionLayer = 0;
        CollisionMask = 0;
    }

    private void RecalculateEffectiveStats()
    {
        _equipmentStats = _equipment.CombinedStats();
        EffectiveStats = _equipmentStats;
        if (_health == null || _baseMaxHealth <= 0)
        {
            return;
        }

        _health.SetMaxHealthPreservingCurrent(
            Mathf.Max(1, _baseMaxHealth + EffectiveStats.MaxHp));
        _health.SetDefensiveStats(EffectiveStats);
    }

    private static float HorizontalDistanceSquared(Vector3 first, Vector3 second)
    {
        var delta = first - second;
        delta.Y = 0.0f;
        return delta.LengthSquared();
    }
}
