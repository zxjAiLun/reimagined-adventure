using System;
using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// First 3D player adapter. It intentionally owns only spatial runtime work;
/// damage, skills, loot and equipment rules stay in Arpg.Domain.
/// </summary>
public partial class PlayerController3D : CharacterBody3D, ICombatTarget
{
    public const uint PlayerCollisionLayer = 2;
    public const uint PlayerCollisionMask = 9;

    [Export] public float PickupRange { get; set; } = 2.4f;
    [Export] public PackedScene ProjectileScene { get; set; }
    [Export] public PackedScene AreaEffectScene { get; set; }

    public Stats EffectiveStats { get; private set; } = Stats.Neutral;
    public Stats RewardStats => _rewardStats;
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
    public Item EquippedWeapon => _equipment.ItemInSlot(EquipmentSlot.Weapon);

    private readonly List<Item> _items = new();
    private readonly Equipment _equipment = new();
    private HealthComponent _health;
    private MouseGroundTargeting3D _targeting;
    private PlayerMotor3D _motor;
    private PlayerSkillController3D _skills;
    private Stats _equipmentStats = Stats.Neutral;
    private Stats _rewardStats = Stats.Neutral;
    private int _baseMaxHealth;

    public override void _Ready()
    {
        AddToGroup("player_3d");
        AddToGroup("damageables_3d");

        _health = GetNode<HealthComponent>("HealthComponent");
        _targeting = GetNode<MouseGroundTargeting3D>("MouseGroundTargeting3D");
        _motor = GetNodeOrNull<PlayerMotor3D>("PlayerMotor3D");
        _skills = GetNodeOrNull<PlayerSkillController3D>("PlayerSkillController3D");
        _targeting.Camera = GetTree().GetFirstNodeInGroup("arena_cameras") as Camera3D;
        _baseMaxHealth = _health.MaxHealth;
        _health.Died += OnDied;
        SetProcessUnhandledInput(true);
        RecalculateEffectiveStats();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsAlive)
        {
            return;
        }

        if (@event.IsActionPressed("pickup_item", true))
        {
            TryPickupNearest();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("equip_item", true))
        {
            TryEquipNewestWeapon();
            GetViewport().SetInputAsHandled();
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
        return CastSpreadShot(SkillLibrary.SpreadShot());
    }

    public bool CastSpreadShot(
        SkillDefinition skill,
        IReadOnlyList<SupportDefinition> supports = null)
    {
        ArgumentNullException.ThrowIfNull(skill);
        if (!IsAlive || skill.CastType != SkillCastType.Projectile || ProjectileScene == null)
        {
            return false;
        }

        var damage = SkillSupportMath.Damage(skill, EffectiveStats, supports);
        var projectileCount = SkillSupportMath.ProjectileCount(skill, EffectiveStats, supports);
        var spread = Mathf.DegToRad((float)SkillSupportMath.SpreadAngle(skill, supports));
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

        return true;
    }

    public bool CastPulse()
    {
        return CastAreaSkill(SkillLibrary.Pulse(), GlobalPosition, AreaEffectScene);
    }

    public bool CastAreaSkill(
        SkillDefinition skill,
        Vector3 targetPosition,
        PackedScene areaEffectScene,
        IReadOnlyList<SupportDefinition> supports = null)
    {
        ArgumentNullException.ThrowIfNull(skill);
        if (!IsAlive || areaEffectScene == null || !skill.IsArea)
        {
            return false;
        }

        var effect = areaEffectScene.Instantiate<SkillAreaEffect3D>();
        GetParent().AddChild(effect);
        effect.Configure(
            skill,
            targetPosition,
            new DamageRequest(
                SkillSupportMath.Damage(skill, EffectiveStats, supports),
                skill.DamageType,
                skill.Id,
                CombatFaction.Player),
            Mathf.Max(1.5f, SpatialScale3D.Distance(
                SkillSupportMath.Radius(skill, EffectiveStats, supports))));
        return true;
    }

    public bool PerformDash(double distance)
    {
        return _motor?.PerformDash(distance) ?? false;
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

    public void SetRewardStats(Stats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        stats.Validate();
        _rewardStats = stats;
        RecalculateEffectiveStats();
    }

    public bool CanRestoreCurrentHealth(int currentHealth)
    {
        return _health != null && currentHealth >= 0 && currentHealth <= MaxHealth;
    }

    public bool TryCalculateMaxHealthForRestore(
        IReadOnlyList<Item> items,
        Item equippedWeapon,
        Stats rewardStats,
        out int maxHealth)
    {
        maxHealth = 0;
        if (!TryBuildRestoreStats(items, equippedWeapon, rewardStats, out var restoreStats))
        {
            return false;
        }

        maxHealth = Mathf.Max(1, _baseMaxHealth + restoreStats.MaxHp);
        return true;
    }

    public void ApplyRestoredHealth(int currentHealth)
    {
        if (!CanRestoreCurrentHealth(currentHealth) || !_health.TryRestoreCurrentHealth(currentHealth))
        {
            throw new ArgumentException("Invalid 3D player health value.", nameof(currentHealth));
        }

        RestoreRuntimeAfterHealthRestore();
    }

    public void ApplyRestoredRuntimeState(int currentHealth)
    {
        ApplyRestoredHealth(currentHealth);
    }

    public bool RestoreInventory(IReadOnlyList<Item> items, Item equippedWeapon)
    {
        if (!TryBuildRestoreStats(items, equippedWeapon, _rewardStats, out _))
        {
            return false;
        }

        _items.Clear();
        _items.AddRange(items);
        _equipment.Reset();
        if (equippedWeapon != null)
        {
            _equipment.Equip(equippedWeapon, 1);
        }

        RecalculateEffectiveStats();
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

    private void OnDied()
    {
        Velocity = Vector3.Zero;
        SetPhysicsProcess(false);
        CollisionLayer = 0;
        CollisionMask = 0;
    }

    private void RestoreRuntimeAfterHealthRestore()
    {
        if (IsAlive)
        {
            SetPhysicsProcess(true);
            CollisionLayer = PlayerCollisionLayer;
            CollisionMask = PlayerCollisionMask;
        }
        else
        {
            SetPhysicsProcess(false);
            CollisionLayer = 0;
            CollisionMask = 0;
        }
    }

    private void RecalculateEffectiveStats()
    {
        _equipmentStats = _equipment.CombinedStats();
        EffectiveStats = Stats.Combine(_equipmentStats, _rewardStats);
        if (_health == null || _baseMaxHealth <= 0)
        {
            return;
        }

        _health.SetMaxHealthPreservingCurrent(
            Mathf.Max(1, _baseMaxHealth + EffectiveStats.MaxHp));
        _health.SetDefensiveStats(EffectiveStats);
    }

    private static bool TryBuildRestoreStats(
        IReadOnlyList<Item> items,
        Item equippedWeapon,
        Stats rewardStats,
        out Stats restoreStats)
    {
        restoreStats = null;
        if (items == null || items.Count > 8 || rewardStats == null)
        {
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item == null || !ids.Add(item.Id))
            {
                return false;
            }

            try
            {
                item.Validate();
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        var restoreEquipment = new Equipment();
        if (equippedWeapon != null)
        {
            if (ids.Contains(equippedWeapon.Id) || !restoreEquipment.CanEquip(equippedWeapon, 1))
            {
                return false;
            }

            restoreEquipment.Equip(equippedWeapon, 1);
        }

        try
        {
            restoreStats = Stats.Combine(restoreEquipment.CombinedStats(), rewardStats);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static float HorizontalDistanceSquared(Vector3 first, Vector3 second)
    {
        var delta = first - second;
        delta.Y = 0.0f;
        return delta.LengthSquared();
    }
}
