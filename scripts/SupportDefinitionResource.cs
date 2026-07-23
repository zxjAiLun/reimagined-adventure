using Arpg.Domain;
using Godot;

/// <summary>
/// Godot authoring adapter for a support gem. Compatibility and numeric
/// modifiers are still validated by the pure Domain SupportDefinition.
/// </summary>
[GlobalClass]
public partial class SupportDefinitionResource : Resource
{
    [Export] public string SupportId { get; set; } = "support";
    [Export] public string DisplayName { get; set; } = "Support";
    [Export] public int Kind { get; set; }
    [Export] public float DamageMultiplier { get; set; } = 1.0f;
    [Export] public float RadiusMultiplier { get; set; } = 1.0f;
    [Export] public float CooldownMultiplier { get; set; } = 1.0f;
    [Export] public float ManaCostMultiplier { get; set; } = 1.0f;
    [Export] public int ExtraProjectileCount { get; set; }
    [Export] public float ExtraSpreadAngleDegrees { get; set; }
    [Export] public int RequiredDamageType { get; set; } = -1;
    [Export] public float ElementalDamageMultiplier { get; set; } = 1.0f;

    public SupportDefinition ToDomain()
    {
        var definition = new SupportDefinition
        {
            Id = SupportId,
            Name = DisplayName,
            Kind = (SupportKind)Kind,
            DamageMultiplier = DamageMultiplier,
            RadiusMultiplier = RadiusMultiplier,
            CooldownMultiplier = CooldownMultiplier,
            ManaCostMultiplier = ManaCostMultiplier,
            ExtraProjectileCount = ExtraProjectileCount,
            ExtraSpreadAngleDegrees = ExtraSpreadAngleDegrees,
            RequiredDamageType = RequiredDamageType < 0
                ? null
                : (DamageType)RequiredDamageType,
            ElementalDamageMultiplier = ElementalDamageMultiplier,
        };
        definition.Validate();
        return definition;
    }
}
