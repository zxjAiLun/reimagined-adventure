namespace Arpg.Domain;

public sealed record BossPhaseDefinition(
    string Id,
    int EnterAtHealthPercent,
    IReadOnlyList<BossAttackKind> AttackPattern,
    double RecoveryMultiplier,
    string? AddWaveId,
    string? HazardProfileId)
{
    public void Validate(IReadOnlySet<BossAttackKind> availableAttacks)
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new ArgumentException("Boss phase id is required.", nameof(Id));
        }

        if (EnterAtHealthPercent is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(EnterAtHealthPercent));
        }

        if (AttackPattern == null || AttackPattern.Count == 0)
        {
            throw new ArgumentException("Boss phases need a non-empty attack pattern.", nameof(AttackPattern));
        }

        if (!double.IsFinite(RecoveryMultiplier) || RecoveryMultiplier <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(RecoveryMultiplier));
        }

        ArgumentNullException.ThrowIfNull(availableAttacks);
        foreach (var attack in AttackPattern)
        {
            if (!Enum.IsDefined(attack) || !availableAttacks.Contains(attack))
            {
                throw new ArgumentException(
                    $"Boss phase '{Id}' references an undefined attack '{attack}'.",
                    nameof(AttackPattern));
            }
        }

        if (!string.IsNullOrWhiteSpace(AddWaveId) && AddWaveId.Length > 80)
        {
            throw new ArgumentException("Boss add wave id is too long.", nameof(AddWaveId));
        }

        if (!string.IsNullOrWhiteSpace(HazardProfileId) && HazardProfileId.Length > 80)
        {
            throw new ArgumentException("Boss hazard profile id is too long.", nameof(HazardProfileId));
        }
    }
}
