using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

[GlobalClass]
public partial class EncounterCatalogResource3D : Resource
{
    [Export] public int CatalogVersion { get; set; } = 1;
    [Export] public Godot.Collections.Array<EncounterCatalogEntryResource3D> Entries { get; set; } = new();

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
            error = "Encounter catalog must contain at least one entry.";
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
                    error = "Encounter catalog contains an invalid entry.";
                }

                return false;
            }

            if (!ids.Add(entry.Definition.EncounterId))
            {
                error = $"Encounter id '{entry.Definition.EncounterId}' is duplicated.";
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
            error = "Encounter catalog must cover map level 1.";
            return false;
        }

        return true;
    }

    public IReadOnlyList<EncounterCandidate> ToDomainCandidates()
    {
        if (!IsValid(out var error))
        {
            throw new System.ArgumentException(error, nameof(Entries));
        }

        return Entries.Select(entry => entry.ToDomainCandidate()).ToArray();
    }

    public EncounterDefinitionResource3D ResolveDefinition(string encounterId)
    {
        if (!IsValid(out var error))
        {
            throw new System.ArgumentException(error, nameof(Entries));
        }

        return Entries.FirstOrDefault(entry => entry.Definition.EncounterId == encounterId)?.Definition
            ?? throw new KeyNotFoundException($"Encounter '{encounterId}' is not in the catalog.");
    }

    public string ResolveDisplayName(string encounterId)
    {
        if (!IsValid(out var error))
        {
            throw new System.ArgumentException(error, nameof(Entries));
        }

        return Entries.FirstOrDefault(entry => entry.Definition.EncounterId == encounterId)?.DisplayName
            ?? throw new KeyNotFoundException($"Encounter '{encounterId}' is not in the catalog.");
    }

    public int ResolveTier(string encounterId)
    {
        if (!IsValid(out var error))
        {
            throw new System.ArgumentException(error, nameof(Entries));
        }

        return Entries.FirstOrDefault(entry => entry.Definition.EncounterId == encounterId)?.EncounterTier
            ?? throw new KeyNotFoundException($"Encounter '{encounterId}' is not in the catalog.");
    }
}
