namespace Arpg.Domain;

/// <summary>
/// The complete input to the shared incoming-damage pipeline.
/// </summary>
public sealed class DamageRequest
{
    public DamageRequest(int rawDamage, DamageType damageType, string sourceId)
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

        RawDamage = rawDamage;
        DamageType = damageType;
        SourceId = sourceId;
    }

    public int RawDamage { get; }
    public DamageType DamageType { get; }
    public string SourceId { get; }
}
