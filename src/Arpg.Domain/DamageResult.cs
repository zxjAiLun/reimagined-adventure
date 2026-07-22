namespace Arpg.Domain;

/// <summary>
/// The outcome reported by a damageable node after mitigation and health loss.
/// </summary>
public readonly record struct DamageResult(int DamageApplied, bool Killed)
{
    public bool WasBlocked => DamageApplied == 0;
}
