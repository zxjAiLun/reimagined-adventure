namespace Arpg.Domain;

/// <summary>
/// A named stat contribution. Affixes are data-only in the domain; rolling and
/// rendering them belong to the loot and Godot adapter layers respectively.
/// </summary>
public sealed class Affix
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int Tier { get; init; } = 1;
    public bool IsPrefix { get; init; }
    public Stats Stats { get; init; } = Stats.Neutral;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new ArgumentException("Affix id cannot be empty.", nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Affix name cannot be empty.", nameof(Name));
        }

        if (Tier < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Tier), "Affix tier must be positive.");
        }

        ArgumentNullException.ThrowIfNull(Stats);
        Stats.Validate();
    }
}
