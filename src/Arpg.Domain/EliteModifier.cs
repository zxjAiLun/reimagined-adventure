namespace Arpg.Domain;

public sealed record EliteDeathEffectDefinition(
    double TelegraphDurationSeconds,
    double ExplosionRadius,
    double DamageMultiplier,
    DamageType DamageType = DamageType.Fire)
{
    public void Validate()
    {
        if (!double.IsFinite(TelegraphDurationSeconds) || TelegraphDurationSeconds <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(TelegraphDurationSeconds),
                "Elite death telegraph duration must be finite and positive.");
        }

        if (!double.IsFinite(ExplosionRadius) || ExplosionRadius <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ExplosionRadius),
                "Elite death explosion radius must be finite and positive.");
        }

        if (!double.IsFinite(DamageMultiplier) || DamageMultiplier < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DamageMultiplier),
                "Elite death damage multiplier must be finite and non-negative.");
        }

        if (!Enum.IsDefined(DamageType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(DamageType),
                DamageType,
                "Unknown damage type.");
        }
    }
}

public sealed record EliteModifierDefinition(
    string Id,
    string Name,
    int MinimumMapLevel,
    int Weight,
    double HealthMultiplier = 1.0,
    double DamageMultiplier = 1.0,
    double MoveSpeedMultiplier = 1.0,
    double ActionSpeedMultiplier = 1.0,
    int ArmorBonus = 0,
    AilmentApplicationDefinition? OnHitAilment = null,
    EliteDeathEffectDefinition? DeathEffect = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Elite modifier id and name are required.");
        }

        if (MinimumMapLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumMapLevel));
        }

        if (Weight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Weight));
        }

        ValidateMultiplier(nameof(HealthMultiplier), HealthMultiplier);
        ValidateMultiplier(nameof(DamageMultiplier), DamageMultiplier);
        ValidateMultiplier(nameof(MoveSpeedMultiplier), MoveSpeedMultiplier);
        ValidateMultiplier(nameof(ActionSpeedMultiplier), ActionSpeedMultiplier);
        if (ArmorBonus < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ArmorBonus));
        }

        OnHitAilment?.Validate();
        DeathEffect?.Validate();
    }

    public bool IsEligible(int mapLevel)
    {
        Validate();
        return mapLevel >= MinimumMapLevel;
    }

    private static void ValidateMultiplier(string name, double value)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Elite multipliers must be finite and positive.");
        }
    }
}

public static class EliteModifierLibrary
{
    private static readonly IReadOnlyList<EliteModifierDefinition> Definitions =
    [
        new EliteModifierDefinition(
            "bulwark",
            "Bulwark",
            MinimumMapLevel: 2,
            Weight: 1,
            HealthMultiplier: 1.50,
            MoveSpeedMultiplier: 0.90,
            ArmorBonus: 8),
        new EliteModifierDefinition(
            "frenzied",
            "Frenzied",
            MinimumMapLevel: 2,
            Weight: 1,
            DamageMultiplier: 1.15,
            MoveSpeedMultiplier: 1.20,
            ActionSpeedMultiplier: 1.25),
        new EliteModifierDefinition(
            "frostbound",
            "Frostbound",
            MinimumMapLevel: 2,
            Weight: 1,
            HealthMultiplier: 1.15,
            OnHitAilment: new AilmentApplicationDefinition(AilmentKind.Chilled, 100)),
        new EliteModifierDefinition(
            "volcanic",
            "Volcanic",
            MinimumMapLevel: 2,
            Weight: 1,
            DamageMultiplier: 1.10,
            DeathEffect: new EliteDeathEffectDefinition(
                TelegraphDurationSeconds: 0.8,
                ExplosionRadius: 1.8,
                DamageMultiplier: 1.0)),
    ];

    static EliteModifierLibrary()
    {
        foreach (var definition in Definitions)
        {
            definition.Validate();
        }
    }

    public static IReadOnlyList<EliteModifierDefinition> All => Definitions;

    public static EliteModifierDefinition? Find(string? id) => string.IsNullOrWhiteSpace(id)
        ? null
        : Definitions.FirstOrDefault(definition => definition.Id == id);
}
