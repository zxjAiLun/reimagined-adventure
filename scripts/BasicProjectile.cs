using System;
using Arpg.Domain;
using Godot;

public partial class BasicProjectile : Area2D
{
    [Export] public float Speed { get; set; } = 500.0f;
    [Export] public float Lifetime { get; set; } = 4.0f;
    [Export] public float Radius { get; set; } = 5.0f;
    [Export] public Color ProjectileColor { get; set; } = new(1.0f, 0.85f, 0.25f, 1.0f);
    [Export] public PackedScene HitEffectScene { get; set; }

    public int Damage { get; private set; }
    public DamageRequest DamageRequest { get; private set; } = new(0, DamageType.Physical, "unconfigured");

    private Vector2 _velocity;
    private float _remainingLifetime;
    private bool _launched;
    private bool _spent;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        _remainingLifetime = Lifetime;
        QueueRedraw();
    }

    public void Launch(Vector2 direction, DamageRequest damageRequest)
    {
        if (direction.LengthSquared() <= 0.001f)
        {
            QueueFree();
            return;
        }

        _velocity = direction.Normalized() * Speed;
        DamageRequest = damageRequest ?? throw new ArgumentNullException(nameof(damageRequest));
        Damage = damageRequest.RawDamage;
        Rotation = _velocity.Angle();
        _launched = true;
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_launched)
        {
            return;
        }

        var frameDelta = (float)delta;
        GlobalPosition += _velocity * frameDelta;
        _remainingLifetime -= frameDelta;
        if (_remainingLifetime <= 0.0f)
        {
            QueueFree();
        }
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, Radius, ProjectileColor);
        DrawLine(
            new Vector2(-Radius * 2.0f, 0.0f),
            new Vector2(Radius * 2.0f, 0.0f),
            ProjectileColor.Lightened(0.30f),
            2.0f);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_spent || body is not IDamageable target || !target.IsAlive)
        {
            return;
        }

        _spent = true;
        Monitoring = false;
        SetPhysicsProcess(false);
        target.ApplyDamage(DamageRequest);
        SpawnHitEffect(GlobalPosition);
        QueueFree();
    }

    private void SpawnHitEffect(Vector2 position)
    {
        if (HitEffectScene == null)
        {
            return;
        }

        var effect = HitEffectScene.Instantiate<HitEffect>();
        GetParent().AddChild(effect);
        effect.GlobalPosition = position;
    }
}
