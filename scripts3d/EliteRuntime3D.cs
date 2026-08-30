using System;
using Arpg.Domain;

public interface IEliteRuntime3D
{
    EliteModifierDefinition EliteModifier { get; }
    string EliteModifierId { get; }
    ulong EliteSelectionSeed { get; }
    int EliteAppliedCount { get; }
}

public static class EliteRuntime3D
{
    public static int ScaleInt(int value, double multiplier)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        if (!double.IsFinite(multiplier) || multiplier < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        }

        var scaled = value * multiplier;
        if (scaled >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return Math.Max(1, (int)Math.Ceiling(scaled));
    }

    public static DamageRequest BuildEnemyAttack(
        EliteModifierDefinition elite,
        int rawDamage,
        DamageType damageType,
        string sourceId)
    {
        ArgumentNullException.ThrowIfNull(elite);
        elite.Validate();
        var ailment = elite.OnHitAilment;
        var durationMultiplier = elite.Id == "frostbound"
            ? AilmentCollection.ChilledDurationSeconds <= 0.0
                ? 1.0
                : 1.5 / AilmentCollection.ChilledDurationSeconds
            : 1.0;
        return new DamageRequest(
            rawDamage,
            damageType,
            sourceId,
            CombatFaction.Enemy,
            false,
            ailment,
            durationMultiplier);
    }

    public static string DisplayTag(EliteModifierDefinition elite) =>
        elite == null ? string.Empty : $"[{elite.Name}]";
}
