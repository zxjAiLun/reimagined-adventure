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
    [Signal]
    public delegate void StatsChangedEventHandler();

    [Signal]
    public delegate void EquipmentChangedEventHandler();

    [Signal]
    public delegate void InventoryChangedEventHandler();

    public const uint PlayerCollisionLayer = 2;
    public const uint PlayerCollisionMask = 9;

    [Export] public float PickupRange { get; set; } = 2.4f;
    [Export] public PackedScene ProjectileScene { get; set; }
    [Export] public PackedScene AreaEffectScene { get; set; }

    public Stats EffectiveStats { get; private set; } = Stats.Neutral;
    public Stats RewardStats => _rewardStats;
    public Stats PassiveStats => _passiveStats;
    public int CurrentHealth => _health?.CurrentHealth ?? 0;
    public int MaxHealth => _health?.MaxHealth ?? 0;
    public bool IsAlive => _health?.IsAlive ?? false;
    public CombatFaction Faction => CombatFaction.Player;
    public Vector3 AimDirection { get; private set; } = Vector3.Forward;
    public AilmentComponent3D Ailments => _ailments;
    public double AilmentMoveSpeedMultiplier => _ailments?.MoveSpeedMultiplier ?? 1.0;
    public double AilmentActionSpeedMultiplier => _ailments?.ActionSpeedMultiplier ?? 1.0;
    public int SpreadShotDamage => SkillSupportMath.Damage(
        SkillLibrary.SpreadShot(),
        EffectiveStats,
        _skills?.Supports(SkillSlot.Primary));
    public int LastSpreadProjectileCount { get; private set; }
    public int LastSpreadProjectileDamage { get; private set; }
    public float LastAreaRadius { get; private set; }
    public int LastAreaDamage { get; private set; }
    public int ItemCount => _inventory.Count;
    public string EquippedWeaponName => _equipment.ItemInSlot(EquipmentSlot.Weapon)?.Name ?? "none";
    public IReadOnlyList<Item> Items => _inventory.Items;
    public IReadOnlyDictionary<EquipmentSlot, Item> EquippedItems => _equipment.Items;
    public RunInventory Inventory => _inventory;
    public Equipment Equipment => _equipment;
    public PlayerSkillController3D Skills => _skills;
    public int InventoryCapacity => _inventory.Capacity;
    public Item EquippedWeapon => _equipment.ItemInSlot(EquipmentSlot.Weapon);

    public TestArena3D GetOwningMap()
    {
        for (var current = GetParent(); current != null; current = current.GetParent())
        {
            if (current is TestArena3D map)
            {
                return map;
            }
        }

        return null;
    }

    private readonly RunInventory _inventory = new(16);
    private readonly Equipment _equipment = new();
    private HealthComponent _health;
    private DamageFeedbackSource3D _damageFeedback;
    private HitFlash3D _hitFlash;
    private DeathFeedback3D _deathFeedback;
    private AilmentComponent3D _ailments;
    private MouseGroundTargeting3D _targeting;
    private PlayerMotor3D _motor;
    private PlayerSkillController3D _skills;
    private PlayerBuildController3D _build;
    private Stats _equipmentStats = Stats.Neutral;
    private Stats _rewardStats = Stats.Neutral;
    private Stats _passiveStats = Stats.Neutral;
    private int _baseMaxHealth;

    public override void _Ready()
    {
        AddToGroup("player_3d");
        AddToGroup("damageables_3d");

        _health = GetNode<HealthComponent>("HealthComponent");
        _damageFeedback = GetNodeOrNull<DamageFeedbackSource3D>("DamageFeedbackSource3D");
        _hitFlash = GetNodeOrNull<HitFlash3D>("HitFlash3D");
        _deathFeedback = GetNodeOrNull<DeathFeedback3D>("DeathFeedback3D");
        _ailments = GetNodeOrNull<AilmentComponent3D>("AilmentComponent3D");
        _targeting = GetNode<MouseGroundTargeting3D>("MouseGroundTargeting3D");
        _motor = GetNodeOrNull<PlayerMotor3D>("PlayerMotor3D");
        _skills = GetNodeOrNull<PlayerSkillController3D>("PlayerSkillController3D");
        _build = GetNodeOrNull<PlayerBuildController3D>("PlayerBuildController3D");
        _targeting.Camera = GetTree().GetFirstNodeInGroup("arena_cameras") as Camera3D;
        _baseMaxHealth = _health.MaxHealth;
        _health.Died += OnDied;
        SetProcessUnhandledInput(true);
        RecalculateEffectiveStats();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_build?.IsOpen == true)
        {
            return;
        }

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

        var incomingRequest = _ailments?.ModifyIncomingDamage(request) ?? request;
        var result = _health.ApplyDamage(incomingRequest);
        if (result.DamageApplied > 0)
        {
            _damageFeedback?.Publish(result);
            _hitFlash?.Trigger();
            _ailments?.ApplyFromDamage(request, result.DamageApplied);
        }

        return result;
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
        LastSpreadProjectileCount = projectileCount;
        LastSpreadProjectileDamage = damage;
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
                    CombatFaction.Player,
                    false,
                    SkillSupportMath.Ailment(skill, EffectiveStats, supports),
                    EffectiveStats.AilmentDurationMultiplier,
                    EffectiveStats.DamageOverTimeMultiplier));
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
        var radius = Mathf.Max(1.5f, SpatialScale3D.Distance(
            SkillSupportMath.Radius(skill, EffectiveStats, supports)));
        LastAreaRadius = radius;
        LastAreaDamage = SkillSupportMath.Damage(skill, EffectiveStats, supports);
        effect.Configure(
            skill,
            targetPosition,
            new DamageRequest(
                SkillSupportMath.Damage(skill, EffectiveStats, supports),
                skill.DamageType,
                skill.Id,
                CombatFaction.Player,
                false,
                SkillSupportMath.Ailment(skill, EffectiveStats, supports),
                EffectiveStats.AilmentDurationMultiplier,
                EffectiveStats.DamageOverTimeMultiplier),
            radius);
        return true;
    }

    public bool PerformDash(double distance)
    {
        return _motor?.PerformDash(distance) ?? false;
    }

    public bool TryAddItem(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var added = _inventory.TryAdd(item, _equipment);
        if (added)
        {
            EmitSignal(SignalName.InventoryChanged);
        }

        return added;
    }

    public void SetRewardStats(Stats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        stats.Validate();
        _rewardStats = stats;
        RecalculateEffectiveStats();
        EmitSignal(SignalName.StatsChanged);
    }

    public void SetPassiveStats(Stats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        stats.Validate();
        _passiveStats = stats;
        RecalculateEffectiveStats();
        EmitSignal(SignalName.StatsChanged);
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
        var equippedItems = equippedWeapon == null
            ? new Dictionary<EquipmentSlot, Item>()
            : new Dictionary<EquipmentSlot, Item> { [EquipmentSlot.Weapon] = equippedWeapon };
        return TryCalculateMaxHealthForRestore(items, equippedItems, rewardStats, out maxHealth);
    }

    public bool TryCalculateMaxHealthForRestore(
        IReadOnlyList<Item> items,
        IReadOnlyDictionary<EquipmentSlot, Item> equippedItems,
        Stats rewardStats,
        out int maxHealth)
    {
        return TryCalculateMaxHealthForRestore(
            items,
            equippedItems,
            rewardStats,
            Stats.Neutral,
            out maxHealth);
    }

    public bool TryCalculateMaxHealthForRestore(
        IReadOnlyList<Item> items,
        IReadOnlyDictionary<EquipmentSlot, Item> equippedItems,
        Stats rewardStats,
        Stats passiveStats,
        out int maxHealth)
    {
        maxHealth = 0;
        if (!TryBuildRestoreStats(items, equippedItems, rewardStats, passiveStats, out var restoreStats))
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
        var equippedItems = equippedWeapon == null
            ? new Dictionary<EquipmentSlot, Item>()
            : new Dictionary<EquipmentSlot, Item> { [EquipmentSlot.Weapon] = equippedWeapon };
        return RestoreEquipment(items, equippedItems);
    }

    public bool RestoreEquipment(
        IReadOnlyList<Item> items,
        IReadOnlyDictionary<EquipmentSlot, Item> equippedItems)
    {
        if (!TryBuildRestoreStats(items, equippedItems, _rewardStats, _passiveStats, out _))
        {
            return false;
        }

        _inventory.Restore(items);
        _equipment.Reset();
        foreach (var pair in equippedItems ?? new Dictionary<EquipmentSlot, Item>())
        {
            _equipment.Equip(pair.Value, 1);
        }

        RecalculateEffectiveStats();
        EmitSignal(SignalName.InventoryChanged);
        EmitSignal(SignalName.EquipmentChanged);
        EmitSignal(SignalName.StatsChanged);
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

            if (!drop.IsOwnedBy(this))
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
        for (var index = _inventory.Items.Count - 1; index >= 0; index--)
        {
            var item = _inventory.Items[index];
            if (item.Slot != EquipmentSlot.Weapon || !_equipment.CanEquip(item, 1))
            {
                continue;
            }

            var result = _inventory.TryEquip(item.Id, _equipment, 1);
            if (!result.Succeeded)
            {
                return false;
            }

            RecalculateEffectiveStats();
            EmitSignal(SignalName.InventoryChanged);
            EmitSignal(SignalName.EquipmentChanged);
            EmitSignal(SignalName.StatsChanged);
            return true;
        }

        return false;
    }

    public bool TryEquipItem(string itemId)
    {
        var result = _inventory.TryEquip(itemId, _equipment, 1);
        if (!result.Succeeded)
        {
            return false;
        }

        RecalculateEffectiveStats();
        EmitSignal(SignalName.InventoryChanged);
        EmitSignal(SignalName.EquipmentChanged);
        EmitSignal(SignalName.StatsChanged);
        return true;
    }

    public bool TryUnequip(EquipmentSlot slot)
    {
        var result = _inventory.TryUnequip(slot, _equipment);
        if (!result.Succeeded)
        {
            return false;
        }

        RecalculateEffectiveStats();
        EmitSignal(SignalName.InventoryChanged);
        EmitSignal(SignalName.EquipmentChanged);
        EmitSignal(SignalName.StatsChanged);
        return true;
    }

    public string InventorySummary()
    {
        return $"Bag {ItemCount}/{InventoryCapacity} | Weapon: {EquippedWeaponName} | Spread damage: {SpreadShotDamage}";
    }

    private void OnDied()
    {
        Velocity = Vector3.Zero;
        SetPhysicsProcess(false);
        CollisionLayer = 0;
        CollisionMask = 0;
        _deathFeedback?.Play();
    }

    private void RestoreRuntimeAfterHealthRestore()
    {
        if (IsAlive)
        {
            _deathFeedback?.ResetPresentation();
            _hitFlash?.ResetPresentation();
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
        EffectiveStats = Stats.Combine(EffectiveStats, _passiveStats);
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
        IReadOnlyDictionary<EquipmentSlot, Item> equippedItems,
        Stats rewardStats,
        Stats passiveStats,
        out Stats restoreStats)
    {
        restoreStats = null;
        if (items == null
            || items.Count > 16
            || rewardStats == null
            || passiveStats == null
            || equippedItems == null)
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
        foreach (var pair in equippedItems)
        {
            if (!Enum.IsDefined(pair.Key)
                || pair.Value == null
                || ids.Contains(pair.Value.Id)
                || pair.Key != pair.Value.Slot
                || !restoreEquipment.CanEquip(pair.Value, 1))
            {
                return false;
            }

            if (!ids.Add(pair.Value.Id))
            {
                return false;
            }

            restoreEquipment.Equip(pair.Value, 1);
        }

        try
        {
            restoreStats = Stats.Combine(
                Stats.Combine(restoreEquipment.CombinedStats(), rewardStats),
                passiveStats);
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
