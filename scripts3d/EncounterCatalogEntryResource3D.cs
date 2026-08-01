using Arpg.Domain;
using Godot;

[GlobalClass]
public partial class EncounterCatalogEntryResource3D : Resource
{
    [Export] public EncounterDefinitionResource3D Definition { get; set; }
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export] public int EncounterTier { get; set; } = 1;
    [Export] public int MinimumMapLevel { get; set; } = 1;
    [Export] public int MaximumMapLevel { get; set; }
    [Export] public int Weight { get; set; } = 1;

    public bool IsValid(out string error)
    {
        error = string.Empty;
        if (Definition == null)
        {
            error = "Definition is required.";
            return false;
        }

        if (!Definition.IsValid(out error))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            error = "DisplayName is required.";
            return false;
        }

        if (EncounterTier < 1)
        {
            error = "EncounterTier must be at least 1.";
            return false;
        }

        if (MinimumMapLevel < 1)
        {
            error = "MinimumMapLevel must be at least 1.";
            return false;
        }

        if (MaximumMapLevel > 0 && MaximumMapLevel < MinimumMapLevel)
        {
            error = "MaximumMapLevel must be 0 or greater than MinimumMapLevel.";
            return false;
        }

        if (Weight <= 0)
        {
            error = "Weight must be positive.";
            return false;
        }

        return true;
    }

    public EncounterCandidate ToDomainCandidate()
    {
        if (!IsValid(out var error))
        {
            throw new System.ArgumentException(error, nameof(Definition));
        }

        return new EncounterCandidate(
            Definition.EncounterId,
            EncounterTier,
            MinimumMapLevel,
            MaximumMapLevel,
            Weight);
    }
}
