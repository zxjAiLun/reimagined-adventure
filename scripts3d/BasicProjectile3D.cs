using System;
using Arpg.Domain;
using Godot;

public partial class BasicProjectile3D : Area3D
{
    [Export] public float Speed { get; set; } = 12.0f;
    [Export] public float Lifetime { get; set; } = 3.0f;

    public DamageRequest DamageRequest { get; private set; } = new(0, DamageType.Physical, "unconfigured_3d");

    private Vector3 _velocity;
    private float _remainingLifetime;
    private bool _launched;
    private bool _spent;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        _remainingLifetime = Lifetime;
    }

    public void Launch(Vector3 direction, DamageRequest damageRequest)
    {
        ArgumentNullException.ThrowIfNull(damageRequest);
        direction.Y = 0.0f;
        if (direction.LengthSquared() <= 0.001f)
        {
            QueueFree();
            return;
        }

        _velocity = direction.Normalized() * Speed;
        DamageRequest = damageRequest;
        _launched = true;
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

    private void OnBodyEntered(Node3D body)
    {
        if (_spent || body is not IDamageable target || !target.IsAlive)
        {
            return;
        }

        _spent = true;
        SetDeferred("monitoring", false);
        SetPhysicsProcess(false);
        target.ApplyDamage(DamageRequest);
        QueueFree();
    }
}
