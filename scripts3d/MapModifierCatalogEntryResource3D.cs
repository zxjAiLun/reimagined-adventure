using Arpg.Domain;
using Godot;

[GlobalClass]
public partial class MapModifierCatalogEntryResource3D : Resource
{
    [Export] public MapModifierResource Modifier { get; set; }
    [Export] public int MinimumMapLevel { get; set; } = 1;
    [Export] public int MaximumMapLevel { get; set; }
    [Export] public int Weight { get; set; } = 1;

    public bool IsValid(out string error)
    {
        error = string.Empty;
        if (Modifier == null)
        {
            error = "Modifier is required.";
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

        try
        {
            Modifier.ToDomain();
        }
        catch (System.Exception exception)
        {
            error = exception.Message;
            return false;
        }

        return true;
    }

    public MapModifierCandidate ToDomainCandidate()
    {
        if (!IsValid(out var error))
        {
            throw new System.ArgumentException(error, nameof(Modifier));
        }

        return new MapModifierCandidate(
            Modifier.ModifierId,
            MinimumMapLevel,
            MaximumMapLevel,
            Weight);
    }
}
