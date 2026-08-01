using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

[GlobalClass]
public partial class MapModifierCatalogResource3D : Resource
{
    [Export] public int CatalogVersion { get; set; } = 1;
    [Export] public Godot.Collections.Array<MapModifierCatalogEntryResource3D> Entries { get; set; } = new();

    public bool IsValid(out string error)
    {
        error = string.Empty;
        if (CatalogVersion < 1)
        {
            error = "CatalogVersion must be positive.";
            return false;
        }

        if (Entries == null || Entries.Count == 0)
        {
            error = "Modifier catalog must contain at least one entry.";
            return false;
        }

        var ids = new HashSet<string>(System.StringComparer.Ordinal);
        var coversMapOne = false;
        foreach (var entry in Entries)
        {
            if (entry == null || !entry.IsValid(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Modifier catalog contains an invalid entry.";
                }

                return false;
            }

            if (!ids.Add(entry.Modifier.ModifierId))
            {
                error = $"Modifier id '{entry.Modifier.ModifierId}' is duplicated.";
                return false;
            }

            if (entry.MinimumMapLevel <= 1
                && (entry.MaximumMapLevel <= 0 || entry.MaximumMapLevel >= 1))
            {
                coversMapOne = true;
            }
        }

        if (!coversMapOne)
        {
            error = "Modifier catalog must cover map level 1.";
            return false;
        }

        return true;
    }

    public IReadOnlyList<MapModifierCandidate> ToDomainCandidates()
    {
        if (!IsValid(out var error))
        {
            throw new System.ArgumentException(error, nameof(Entries));
        }

        return Entries.Select(entry => entry.ToDomainCandidate()).ToArray();
    }

    public MapModifierDefinition ResolveDefinition(string modifierId)
    {
        if (!IsValid(out var error))
        {
            throw new System.ArgumentException(error, nameof(Entries));
        }

        var entry = Entries.FirstOrDefault(candidate => candidate.Modifier.ModifierId == modifierId)
            ?? throw new KeyNotFoundException($"Modifier '{modifierId}' is not in the catalog.");
        return entry.Modifier.ToDomain();
    }
}
