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
        var previousPosition = GlobalPosition;
        var nextPosition = previousPosition + _velocity * frameDelta;
        if (TrySweepForHit(previousPosition, nextPosition))
        {
            return;
        }

        GlobalPosition = nextPosition;
        _remainingLifetime -= frameDelta;
        if (_remainingLifetime <= 0.0f)
        {
            QueueFree();
        }
    }

    private void OnBodyEntered(Node3D body)
    {
        TryHit(body);
    }

    private bool TrySweepForHit(Vector3 previousPosition, Vector3 nextPosition)
    {
        var world = GetWorld3D();
        if (world == null)
        {
            return false;
        }

        var query = PhysicsRayQueryParameters3D.Create(
            previousPosition,
            nextPosition,
            CollisionMask);
        query.CollideWithBodies = true;
        query.CollideWithAreas = false;
        var result = world.DirectSpaceState.IntersectRay(query);
        if (result.Count == 0
            || !result.TryGetValue("collider", out var collider)
            || collider.AsGodotObject() is not Node3D body)
        {
            return false;
        }

        return TryHit(body);
    }

    private bool TryHit(Node3D body)
    {
        if (_spent || body is not ICombatTarget target || !target.IsAlive)
        {
            return false;
        }

        if (!CombatTargeting.CanHit(DamageRequest, target))
        {
            return false;
        }

        _spent = true;
        SetDeferred("monitoring", false);
        SetPhysicsProcess(false);
        target.ApplyDamage(DamageRequest);
        QueueFree();
        return true;
    }
}
