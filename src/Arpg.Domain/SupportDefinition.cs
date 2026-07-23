namespace Arpg.Domain;

public enum SupportKind
{
    Pierce,
    Amplify,
    Quickcast,
    Volley,
    Trailblazer,
    Concentration,
    Echo,
    ElementalFocus,
}

/// <summary>
/// Minimal support data migrated from the C++ support contract. A support is
/// compatible with a skill before any numeric modifier is applied.
/// </summary>
public sealed class SupportDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public SupportKind Kind { get; init; }
    public double DamageMultiplier { get; init; } = 1.0;
    public double RadiusMultiplier { get; init; } = 1.0;
    public double CooldownMultiplier { get; init; } = 1.0;
    public double ManaCostMultiplier { get; init; } = 1.0;
    public int ExtraProjectileCount { get; init; }
    public double ExtraSpreadAngleDegrees { get; init; }
    public DamageType? RequiredDamageType { get; init; }
    public double ElementalDamageMultiplier { get; init; } = 1.0;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new ArgumentException("Support id cannot be empty.", nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Support name cannot be empty.", nameof(Name));
        }

        if (!Enum.IsDefined(Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown support kind.");
        }

        if (ExtraProjectileCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ExtraProjectileCount), "Extra projectiles cannot be negative.");
        }

        ValidateMultiplier(nameof(DamageMultiplier), DamageMultiplier);
        ValidateMultiplier(nameof(RadiusMultiplier), RadiusMultiplier);
        ValidateMultiplier(nameof(CooldownMultiplier), CooldownMultiplier);
        ValidateMultiplier(nameof(ManaCostMultiplier), ManaCostMultiplier);
        ValidateMultiplier(nameof(ElementalDamageMultiplier), ElementalDamageMultiplier);
        if (RequiredDamageType.HasValue && !Enum.IsDefined(RequiredDamageType.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(RequiredDamageType), RequiredDamageType, "Unknown required damage type.");
        }
        if (!double.IsFinite(ExtraSpreadAngleDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(ExtraSpreadAngleDegrees));
        }
    }

    private static void ValidateMultiplier(string name, double value)
    {
        if (!double.IsFinite(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Multiplier must be finite and non-negative.");
        }
    }
}

public static class SupportLibrary
{
    private static readonly SupportDefinition[] Supports =
    [
        new SupportDefinition
        {
            Id = "pierce",
            Name = "Pierce",
            Kind = SupportKind.Pierce,
        },
        new SupportDefinition
        {
            Id = "amplify",
            Name = "Amplify",
            Kind = SupportKind.Amplify,
            RadiusMultiplier = 1.35,
            CooldownMultiplier = 1.20,
        },
        new SupportDefinition
        {
            Id = "quickcast",
            Name = "Quickcast",
            Kind = SupportKind.Quickcast,
            DamageMultiplier = 0.85,
            CooldownMultiplier = 0.70,
        },
        new SupportDefinition
        {
            Id = "volley",
            Name = "Volley",
            Kind = SupportKind.Volley,
            DamageMultiplier = 0.80,
            ExtraProjectileCount = 2,
            ExtraSpreadAngleDegrees = 22.0,
        },
        new SupportDefinition
        {
            Id = "trailblazer",
            Name = "Trailblazer",
            Kind = SupportKind.Trailblazer,
        },
        new SupportDefinition
        {
            Id = "concentration",
            Name = "Concentration",
            Kind = SupportKind.Concentration,
            DamageMultiplier = 1.22,
            RadiusMultiplier = 0.78,
            CooldownMultiplier = 1.12,
        },
        new SupportDefinition
        {
            Id = "echo",
            Name = "Echo",
            Kind = SupportKind.Echo,
            DamageMultiplier = 0.65,
            CooldownMultiplier = 1.35,
        },
        new SupportDefinition
        {
            Id = "ember_focus",
            Name = "Ember Focus",
            Kind = SupportKind.ElementalFocus,
            RequiredDamageType = DamageType.Fire,
            ElementalDamageMultiplier = 1.28,
        },
    ];

    public static IReadOnlyList<SupportDefinition> All => Supports;

    public static SupportDefinition? Find(string? id)
    {
        return string.IsNullOrWhiteSpace(id)
            ? null
            : Supports.FirstOrDefault(support => support.Id == id);
    }

    public static bool SupportsSkill(SupportDefinition support, SkillDefinition skill)
    {
        ArgumentNullException.ThrowIfNull(support);
        ArgumentNullException.ThrowIfNull(skill);
        support.Validate();
        skill.Validate();

        return support.Kind switch
        {
            SupportKind.Pierce or SupportKind.Volley => skill.CastType == SkillCastType.Projectile,
            SupportKind.Amplify or SupportKind.Concentration or SupportKind.Echo => skill.IsArea,
            SupportKind.Quickcast => skill.CastType != SkillCastType.Dash,
            SupportKind.Trailblazer => skill.CastType == SkillCastType.Dash,
            SupportKind.ElementalFocus => support.RequiredDamageType == skill.DamageType,
            _ => false,
        };
    }
}
