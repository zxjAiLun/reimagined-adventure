namespace Arpg.Domain;

/// <summary>
/// Godot actors implement this contract without exposing their node type to
/// projectiles, area effects, or the pure combat rules.
/// </summary>
public interface IDamageable
{
    bool IsAlive { get; }

    DamageResult ApplyDamage(DamageRequest request);
}
