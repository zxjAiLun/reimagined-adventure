namespace Arpg.Domain;

public sealed record EncounterCandidate(
    string EncounterId,
    int EncounterTier,
    int MinimumMapLevel = 1,
    int MaximumMapLevel = 0,
    int Weight = 1)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EncounterId))
        {
            throw new ArgumentException("Encounter id is required.", nameof(EncounterId));
        }

        if (EncounterTier < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(EncounterTier));
        }

        if (MinimumMapLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumMapLevel));
        }

        if (MaximumMapLevel > 0 && MaximumMapLevel < MinimumMapLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumMapLevel));
        }

        if (Weight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Weight));
        }
    }

    public bool IsEligible(int mapLevel)
    {
        Validate();
        return mapLevel >= MinimumMapLevel
            && (MaximumMapLevel <= 0 || mapLevel <= MaximumMapLevel);
    }
}

public sealed record EncounterSelectionResult(
    string EncounterId,
    int EncounterTier,
    ulong SelectionSeed,
    int CandidateIndex);

public static class EncounterSelection
{
    private const ulong EncounterStreamNamespace = 0x454E434F554E5445UL;

    public static EncounterSelectionResult Select(
        ulong runSeed,
        int mapLevel,
        int catalogVersion,
        IReadOnlyList<EncounterCandidate> candidates)
    {
        if (mapLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(mapLevel));
        }

        if (catalogVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(catalogVersion));
        }

        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0)
        {
            throw new ArgumentException("Encounter catalog must contain at least one candidate.", nameof(candidates));
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var eligibleIndices = new List<int>();
        var weights = new List<int>();
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index]
                ?? throw new ArgumentException("Encounter catalog cannot contain null candidates.", nameof(candidates));
            candidate.Validate();
            if (!ids.Add(candidate.EncounterId))
            {
                throw new ArgumentException(
                    $"Encounter id '{candidate.EncounterId}' is duplicated.",
                    nameof(candidates));
            }

            if (candidate.IsEligible(mapLevel))
            {
                eligibleIndices.Add(index);
                weights.Add(candidate.Weight);
            }
        }

        if (eligibleIndices.Count == 0)
        {
            throw new ArgumentException(
                $"Encounter catalog has no candidate for map level {mapLevel}.",
                nameof(candidates));
        }

        var namespaceSeed = RandomService.DeriveSeed(runSeed, EncounterStreamNamespace);
        var mapStream = unchecked(
            ((ulong)(uint)catalogVersion << 32)
            | (uint)mapLevel);
        var selectionSeed = RandomService.DeriveSeed(namespaceSeed, mapStream);
        var random = new RandomService(selectionSeed);
        var eligibleIndex = random.WeightedChoiceIndex(weights);
        var candidateIndex = eligibleIndices[eligibleIndex];
        var selected = candidates[candidateIndex];
        return new EncounterSelectionResult(
            selected.EncounterId,
            selected.EncounterTier,
            selectionSeed,
            candidateIndex);
    }
}
