namespace Arpg.Domain;

/// <summary>
/// The complete input to the shared incoming-damage pipeline.
/// </summary>
public sealed class DamageRequest
{
    public DamageRequest(int rawDamage, DamageType damageType, string sourceId)
        : this(rawDamage, damageType, sourceId, CombatFaction.Neutral, false)
    {
    }

    public DamageRequest(
        int rawDamage,
        DamageType damageType,
        string sourceId,
        CombatFaction sourceFaction,
        bool canHitSameFaction = false,
        AilmentApplicationDefinition? ailment = null,
        double ailmentDurationMultiplier = 1.0,
        double damageOverTimeMultiplier = 1.0)
    {
        if (rawDamage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rawDamage), "Raw damage cannot be negative.");
        }

        if (!Enum.IsDefined(damageType))
        {
            throw new ArgumentOutOfRangeException(nameof(damageType), damageType, "Unknown damage type.");
        }

        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("Damage source id cannot be empty.", nameof(sourceId));
        }

        if (!Enum.IsDefined(sourceFaction))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceFaction), sourceFaction, "Unknown combat faction.");
        }

        if (!double.IsFinite(ailmentDurationMultiplier) || ailmentDurationMultiplier < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ailmentDurationMultiplier),
                "Ailment duration multiplier must be finite and non-negative.");
        }

        if (!double.IsFinite(damageOverTimeMultiplier) || damageOverTimeMultiplier < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(damageOverTimeMultiplier),
                "Damage over time multiplier must be finite and non-negative.");
        }

        ailment?.Validate();

        RawDamage = rawDamage;
        DamageType = damageType;
        SourceId = sourceId;
        SourceFaction = sourceFaction;
        CanHitSameFaction = canHitSameFaction;
        Ailment = ailment;
        AilmentDurationMultiplier = ailmentDurationMultiplier;
        DamageOverTimeMultiplier = damageOverTimeMultiplier;
    }

    public int RawDamage { get; }
    public DamageType DamageType { get; }
    public string SourceId { get; }
    public CombatFaction SourceFaction { get; }
    public bool CanHitSameFaction { get; }
    public AilmentApplicationDefinition? Ailment { get; }
    public double AilmentDurationMultiplier { get; }
    public double DamageOverTimeMultiplier { get; }

    public DamageRequest WithRawDamage(int rawDamage) => new(
        rawDamage,
        DamageType,
        SourceId,
        SourceFaction,
        CanHitSameFaction,
        Ailment,
        AilmentDurationMultiplier,
        DamageOverTimeMultiplier);
}
