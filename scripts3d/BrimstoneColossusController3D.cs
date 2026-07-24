using System;
using Arpg.Domain;
using Godot;

public enum BrimstoneColossusState3D
{
    Idle,
    Chasing,
    PreparingSlam,
    Recovering,
    PreparingSpear,
    Dead,
}

/// <summary>
/// 3D presentation adapter for the Domain Brimstone definition. The state
/// machine and attack names mirror the stable 2D behavior while delivery uses
/// 3D telegraphs and the faction-aware projectile/area adapters.
/// </summary>
public partial class BrimstoneColossusController3D : CharacterBody3D, ICombatTarget
{
    [Export] public BossDefinitionResource DefinitionResource { get; set; }
    [Export] public float Radius { get; set; } = 1.2f;
    [Export] public PackedScene ProjectileScene { get; set; }
    [Export] public PackedScene AreaEffectScene { get; set; }
    [Export] public PackedScene ItemDropScene { get; set; }

    public CombatFaction Faction => CombatFaction.Enemy;
    public int CurrentHealth => _health?.CurrentHealth ?? 0;
    public int MaxHealth => _health?.MaxHealth ?? 0;
    public bool IsAlive => _health?.IsAlive ?? false;
    public BrimstoneColossusState3D State { get; private set; } = BrimstoneColossusState3D.Idle;
    public int MagmaSlamCount { get; private set; }
    public int FlameSpearCount { get; private set; }

    private HealthComponent _health;
    private PlayerController3D _player;
    private RunSessionNode _runSession;
    private Label3D _healthLabel;
    private MeshInstance3D _telegraph;
    private float _moveSpeed;
    private float _slamDamage;
    private float _slamPreparationSeconds;
    private float _slamRadius;
    private float _slamRange;
    private float _spearDamage;
    private float _spearPreparationSeconds;
    private float _spearRange;
    private float _recoverySeconds;
    private float _stateRemaining;
    private bool _nextAttackIsSlam = true;
    private bool _deathHandled;

    public override void _Ready()
    {
        AddToGroup("damageables_3d");
        AddToGroup("enemies_3d");
        AddToGroup("bosses_3d");
        _health = GetNode<HealthComponent>("HealthComponent");
        _health.Died += OnDied;
        _healthLabel = GetNodeOrNull<Label3D>("HealthLabel");
        _telegraph = GetNodeOrNull<MeshInstance3D>("Telegraph");
        _runSession = GetTree().GetFirstNodeInGroup("run_sessions") as RunSessionNode;
        ApplyDefinition(DefinitionResource?.ToDomain() ?? BossLibrary.BrimstoneColossus());
        FindPlayer();
        RefreshVisuals();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsAlive || State == BrimstoneColossusState3D.Dead)
        {
            return;
        }

        if (_player == null || !GodotObject.IsInstanceValid(_player))
        {
            _player = GetTree().GetFirstNodeInGroup("player_3d") as PlayerController3D;
        }

        if (_player == null || !GodotObject.IsInstanceValid(_player) || !_player.IsAlive)
        {
            Velocity = Vector3.Zero;
            return;
        }

        var frameDelta = (float)delta;
        switch (State)
        {
            case BrimstoneColossusState3D.Idle:
            case BrimstoneColossusState3D.Chasing:
                TryChooseAttack();
                break;
            case BrimstoneColossusState3D.PreparingSlam:
            case BrimstoneColossusState3D.PreparingSpear:
                Velocity = Vector3.Zero;
                _stateRemaining -= frameDelta;
                if (_stateRemaining <= 0.0f)
                {
                    if (State == BrimstoneColossusState3D.PreparingSpear)
                    {
                        FireFlameSpear();
                    }

                    State = BrimstoneColossusState3D.Recovering;
                    _stateRemaining = _recoverySeconds;
                }

                break;
            case BrimstoneColossusState3D.Recovering:
                Velocity = Vector3.Zero;
                _stateRemaining -= frameDelta;
                if (_stateRemaining <= 0.0f)
                {
                    State = BrimstoneColossusState3D.Idle;
                }

                break;
            case BrimstoneColossusState3D.Dead:
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

    private void TryChooseAttack()
    {
        var toPlayer = _player.GlobalPosition - GlobalPosition;
        toPlayer.Y = 0.0f;
        var distance = toPlayer.Length();
        if (_nextAttackIsSlam && distance <= _slamRange)
        {
            BeginMagmaSlam();
            return;
        }

        if (!_nextAttackIsSlam && distance <= _spearRange)
        {
            BeginFlameSpear();
            return;
        }

        State = BrimstoneColossusState3D.Chasing;
        if (toPlayer.LengthSquared() > 0.001f)
        {
            Velocity = toPlayer.Normalized() * _moveSpeed;
            MoveAndSlide();
        }
    }

    private void BeginMagmaSlam()
    {
        State = BrimstoneColossusState3D.PreparingSlam;
        MagmaSlamCount++;
        _stateRemaining = Mathf.Max(0.05f, _slamPreparationSeconds);
        _nextAttackIsSlam = false;
        if (AreaEffectScene == null)
        {
            return;
        }

        var effect = AreaEffectScene.Instantiate<SkillAreaEffect3D>();
        GetParent().AddChild(effect);
        effect.ConfigureHazard(
            GlobalPosition,
            _slamRadius,
            _slamPreparationSeconds,
            0.30,
            new DamageRequest(
                (int)_slamDamage,
                DamageType.Fire,
                "magma_slam_3d",
                CombatFaction.Enemy));
    }

    private void BeginFlameSpear()
    {
        State = BrimstoneColossusState3D.PreparingSpear;
        FlameSpearCount++;
        _stateRemaining = Mathf.Max(0.05f, _spearPreparationSeconds);
        _nextAttackIsSlam = true;
    }

    private void FireFlameSpear()
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player)
            || ProjectileScene == null || !_player.IsAlive)
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
        projectile.GlobalPosition = GlobalPosition + direction.Normalized() * (Radius + 0.2f) + Vector3.Up * 0.45f;
        projectile.Launch(
            direction,
            new DamageRequest(
                (int)_spearDamage,
                DamageType.Fire,
                "flame_spear_3d",
                CombatFaction.Enemy));
    }

    private void OnDied()
    {
        if (_deathHandled)
        {
            return;
        }

        _deathHandled = true;
        State = BrimstoneColossusState3D.Dead;
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
            GD.PushError("BrimstoneColossus3D cannot drop loot without a RunSessionNode.");
            return;
        }

        var drop = ItemDropScene.Instantiate<ItemDrop3D>();
        GetParent().AddChild(drop);
        drop.GlobalPosition = GlobalPosition;
        drop.Configure(_runSession.GenerateWeaponDrop(_runSession.CurrentMapLevel, boss: true));
    }

    private void RefreshVisuals()
    {
        if (_healthLabel != null)
        {
            _healthLabel.Text = $"BRIMSTONE COLOSSUS {CurrentHealth}/{MaxHealth}\n{State}";
        }

        if (_telegraph == null)
        {
            return;
        }

        var visible = State == BrimstoneColossusState3D.PreparingSpear
            && _player != null
            && _player.IsAlive;
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

        _telegraph.GlobalPosition = GlobalPosition + Vector3.Up * 0.7f + direction * 0.5f;
        _telegraph.Scale = new Vector3(1.0f, 1.0f, distance);
        _telegraph.LookAt(GlobalPosition + Vector3.Up * 0.7f + direction, Vector3.Up);
    }

    private void ApplyDefinition(BossDefinition definition)
    {
        _moveSpeed = SpatialScale3D.Distance(definition.MoveSpeed);
        _recoverySeconds = (float)definition.RecoverySeconds;
        var slam = definition.Attack(BossAttackKind.MagmaSlam);
        var spear = definition.Attack(BossAttackKind.FlameSpear);
        _slamDamage = slam.Damage;
        _slamPreparationSeconds = (float)slam.PreparationSeconds;
        _slamRadius = SpatialScale3D.Distance(slam.Radius);
        _slamRange = SpatialScale3D.Distance(slam.Range);
        _spearDamage = spear.Damage;
        _spearPreparationSeconds = (float)spear.PreparationSeconds;
        _spearRange = SpatialScale3D.Distance(spear.Range);
        _health.SetMaxHealth(definition.MaxHealth);
        _health.SetDefensiveStats(new Stats { FireResistance = definition.FireResistance });
    }
}
