namespace Arpg.Domain;

public sealed record MapModifierCandidate(
    string ModifierId,
    int MinimumMapLevel = 1,
    int MaximumMapLevel = 0,
    int Weight = 1)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ModifierId))
        {
            throw new ArgumentException("Modifier id is required.", nameof(ModifierId));
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

public sealed record MapModifierSelectionResult(
    string ModifierId,
    ulong SelectionSeed,
    int CandidateIndex);

public static class MapModifierSelection
{
    public static MapModifierSelectionResult Select(
        ulong runSeed,
        int mapLevel,
        int catalogVersion,
        IReadOnlyList<MapModifierCandidate> candidates)
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
            throw new ArgumentException("Modifier catalog must contain at least one candidate.", nameof(candidates));
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var eligibleIndices = new List<int>();
        var weights = new List<int>();
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index]
                ?? throw new ArgumentException("Modifier catalog cannot contain null candidates.", nameof(candidates));
            candidate.Validate();
            if (!ids.Add(candidate.ModifierId))
            {
                throw new ArgumentException($"Modifier id '{candidate.ModifierId}' is duplicated.", nameof(candidates));
            }

            if (candidate.IsEligible(mapLevel))
            {
                eligibleIndices.Add(index);
                weights.Add(candidate.Weight);
            }
        }

        if (eligibleIndices.Count == 0)
        {
            throw new ArgumentException($"Modifier catalog has no candidate for map level {mapLevel}.", nameof(candidates));
        }

        var selectionStream = unchecked(
            ((ulong)(uint)catalogVersion << 32)
            | (uint)mapLevel);
        var selectionSeed = RandomService.DeriveSeed(runSeed, selectionStream);
        var random = new RandomService(selectionSeed);
        var eligibleIndex = random.WeightedChoiceIndex(weights);
        var candidateIndex = eligibleIndices[eligibleIndex];
        return new MapModifierSelectionResult(
            candidates[candidateIndex].ModifierId,
            selectionSeed,
            candidateIndex);
    }
}
