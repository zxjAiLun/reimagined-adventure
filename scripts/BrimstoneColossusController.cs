using System;
using Arpg.Domain;
using Godot;

public enum BrimstoneColossusState
{
    Idle,
    Chasing,
    PreparingSlam,
    Recovering,
    PreparingSpear,
    Dead,
}

public partial class BrimstoneColossusController : CharacterBody2D, IDamageable
{
    [Export] public BossDefinitionResource DefinitionResource { get; set; }
    [Export] public float MoveSpeed { get; set; } = 55.0f;
    [Export] public float Radius { get; set; } = 42.0f;
    [Export] public float SlamRadius { get; set; } = 130.0f;
    [Export] public float SlamRange { get; set; } = 165.0f;
    [Export] public int SlamDamage { get; set; } = 14;
    [Export] public float SlamPreparationSeconds { get; set; } = 0.75f;
    [Export] public float SpearRange { get; set; } = 520.0f;
    [Export] public int SpearDamage { get; set; } = 10;
    [Export] public float SpearPreparationSeconds { get; set; } = 0.55f;
    [Export] public float RecoverySeconds { get; set; } = 0.65f;
    [Export] public PackedScene ProjectileScene { get; set; }
    [Export] public PackedScene AreaEffectScene { get; set; }

    public int CurrentHealth => _health?.CurrentHealth ?? 0;
    public int MaxHealth => _health?.MaxHealth ?? 0;
    public bool IsAlive => _health?.IsAlive ?? false;
    public BrimstoneColossusState State { get; private set; } = BrimstoneColossusState.Idle;
    public int MagmaSlamCount { get; private set; }
    public int FlameSpearCount { get; private set; }

    private HealthComponent _health;
    private PlayerController _player;
    private float _stateRemaining;
    private bool _nextAttackIsSlam = true;
    private bool _deathHandled;
    private Label _healthLabel;
    private AnimationPlayer _animationPlayer;
    private BossDefinition _definition;

    public override void _Ready()
    {
        AddToGroup("damageables");
        AddToGroup("enemies");
        AddToGroup("bosses");

        _health = GetNode<HealthComponent>("HealthComponent");
        _health.Died += OnDied;
        _definition = DefinitionResource?.ToDomain() ?? BossLibrary.BrimstoneColossus();
        ApplyDefinition(_definition);
        _healthLabel = GetNodeOrNull<Label>("HealthLabel");
        _animationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        FindPlayer();
        RefreshVisuals();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsAlive || State == BrimstoneColossusState.Dead)
        {
            return;
        }

        if (_player == null || !_player.IsAlive)
        {
            FindPlayer();
            Velocity = Vector2.Zero;
            return;
        }

        var frameDelta = (float)delta;
        switch (State)
        {
            case BrimstoneColossusState.Idle:
            case BrimstoneColossusState.Chasing:
                TryChooseAttack();
                break;
            case BrimstoneColossusState.PreparingSlam:
                Velocity = Vector2.Zero;
                _stateRemaining -= frameDelta;
                if (_stateRemaining <= 0.0f)
                {
                    State = BrimstoneColossusState.Recovering;
                    _stateRemaining = RecoverySeconds;
                }
                break;
            case BrimstoneColossusState.PreparingSpear:
                Velocity = Vector2.Zero;
                _stateRemaining -= frameDelta;
                if (_stateRemaining <= 0.0f)
                {
                    FireFlameSpear();
                    State = BrimstoneColossusState.Recovering;
                    _stateRemaining = RecoverySeconds;
                }
                break;
            case BrimstoneColossusState.Recovering:
                Velocity = Vector2.Zero;
                _stateRemaining -= frameDelta;
                if (_stateRemaining <= 0.0f)
                {
                    State = BrimstoneColossusState.Idle;
                }
                break;
            case BrimstoneColossusState.Dead:
                break;
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
        var bodyColor = State == BrimstoneColossusState.Dead
            ? new Color(0.22f, 0.20f, 0.20f, 1.0f)
            : new Color(0.72f, 0.24f, 0.12f, 1.0f);
        DrawCircle(Vector2.Zero, Radius, bodyColor);
        DrawArc(Vector2.Zero, Radius, 0.0f, Mathf.Tau, 32, new Color(1.0f, 0.62f, 0.24f, 1.0f), 4.0f);

        if (State == BrimstoneColossusState.PreparingSlam)
        {
            DrawCircle(Vector2.Zero, SlamRadius, new Color(1.0f, 0.22f, 0.04f, 0.10f));
            DrawArc(Vector2.Zero, SlamRadius, 0.0f, Mathf.Tau, 64, new Color(1.0f, 0.34f, 0.08f, 0.85f), 4.0f);
        }

        var direction = _player != null
            ? (_player.GlobalPosition - GlobalPosition).Normalized()
            : Vector2.Left;
        var aimColor = State == BrimstoneColossusState.PreparingSpear
            ? new Color(1.0f, 0.84f, 0.24f, 0.95f)
            : new Color(1.0f, 0.64f, 0.30f, 0.85f);
        DrawLine(Vector2.Zero, direction * (Radius + 18.0f), aimColor, 5.0f);

        var healthRatio = MaxHealth > 0 ? (float)CurrentHealth / MaxHealth : 0.0f;
        DrawRect(new Rect2(-Radius, Radius + 12.0f, Radius * 2.0f, 7.0f), new Color(0.10f, 0.08f, 0.08f, 1.0f));
        DrawRect(new Rect2(-Radius, Radius + 12.0f, Radius * 2.0f * healthRatio, 7.0f), new Color(1.0f, 0.28f, 0.08f, 1.0f));
    }

    private void FindPlayer()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
    }

    private void TryChooseAttack()
    {
        if (_player == null || !_player.IsAlive)
        {
            return;
        }

        var toPlayer = _player.GlobalPosition - GlobalPosition;
        var distance = toPlayer.Length();
        if (_nextAttackIsSlam && distance <= SlamRange)
        {
            BeginMagmaSlam();
            return;
        }

        if (!_nextAttackIsSlam && distance <= SpearRange)
        {
            BeginFlameSpear();
            return;
        }

        State = BrimstoneColossusState.Chasing;
        if (toPlayer.LengthSquared() > 0.001f)
        {
            Velocity = toPlayer.Normalized() * MoveSpeed;
            MoveAndSlide();
        }
    }

    private void BeginMagmaSlam()
    {
        State = BrimstoneColossusState.PreparingSlam;
        MagmaSlamCount++;
        _stateRemaining = Mathf.Max(0.05f, SlamPreparationSeconds);
        _nextAttackIsSlam = false;
        _animationPlayer?.Play("magma_slam");

        if (AreaEffectScene == null)
        {
            return;
        }

        var effect = AreaEffectScene.Instantiate<EnemyAreaEffect>();
        effect.Configure(
            GlobalPosition,
            SlamRadius,
            SlamPreparationSeconds,
            0.30f,
            new DamageRequest(SlamDamage, DamageType.Fire, "magma_slam"),
            new Color(1.0f, 0.20f, 0.04f, 1.0f));
        GetParent().AddChild(effect);
    }

    private void BeginFlameSpear()
    {
        State = BrimstoneColossusState.PreparingSpear;
        FlameSpearCount++;
        _stateRemaining = Mathf.Max(0.05f, SpearPreparationSeconds);
        _nextAttackIsSlam = true;
        _animationPlayer?.Play("flame_spear");
    }

    private void FireFlameSpear()
    {
        if (_player == null || ProjectileScene == null || !_player.IsAlive)
        {
            return;
        }

        var direction = (_player.GlobalPosition - GlobalPosition).Normalized();
        var projectile = ProjectileScene.Instantiate<BasicProjectile>();
        GetParent().AddChild(projectile);
        projectile.GlobalPosition = GlobalPosition + direction * (Radius + 10.0f);
        projectile.Launch(
            direction,
            new DamageRequest(SpearDamage, DamageType.Fire, "flame_spear"));
    }

    private void OnDied()
    {
        if (_deathHandled)
        {
            return;
        }

        _deathHandled = true;
        State = BrimstoneColossusState.Dead;
        _animationPlayer?.Stop();
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
            _healthLabel.Text = $"BRIMSTONE COLOSSUS {CurrentHealth}/{MaxHealth}\n{State}";
        }

        QueueRedraw();
    }

    private void ApplyDefinition(BossDefinition definition)
    {
        MoveSpeed = (float)definition.MoveSpeed;
        RecoverySeconds = (float)definition.RecoverySeconds;
        SlamDamage = definition.Attack(BossAttackKind.MagmaSlam).Damage;
        SlamPreparationSeconds = (float)definition.Attack(BossAttackKind.MagmaSlam).PreparationSeconds;
        SlamRadius = (float)definition.Attack(BossAttackKind.MagmaSlam).Radius;
        SlamRange = (float)definition.Attack(BossAttackKind.MagmaSlam).Range;
        SpearDamage = definition.Attack(BossAttackKind.FlameSpear).Damage;
        SpearPreparationSeconds = (float)definition.Attack(BossAttackKind.FlameSpear).PreparationSeconds;
        SpearRange = (float)definition.Attack(BossAttackKind.FlameSpear).Range;
        _health.SetMaxHealth(definition.MaxHealth);
        _health.SetDefensiveStats(new Stats { FireResistance = definition.FireResistance });
    }
}
