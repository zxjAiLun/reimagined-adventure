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
        bool canHitSameFaction = false)
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

        RawDamage = rawDamage;
        DamageType = damageType;
        SourceId = sourceId;
        SourceFaction = sourceFaction;
        CanHitSameFaction = canHitSameFaction;
    }

    public int RawDamage { get; }
    public DamageType DamageType { get; }
    public string SourceId { get; }
    public CombatFaction SourceFaction { get; }
    public bool CanHitSameFaction { get; }
}
