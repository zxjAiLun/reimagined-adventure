namespace Arpg.Domain;

/// <summary>
/// Damageable target with explicit combat-team semantics. The Godot layer
/// implements this interface; the filtering rule remains portable and testable.
/// </summary>
public interface ICombatTarget : IDamageable
{
    CombatFaction Faction { get; }
}
