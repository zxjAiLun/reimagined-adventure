namespace Arpg.Domain;

public sealed record EliteSelectionResult(
    string? EliteModifierId,
    ulong SelectionSeed);

public static class EliteSelection
{
    private const ulong EliteStreamNamespace = 0x454C495445535452UL;

    public static int EligibilityRatePercent(int mapLevel)
    {
        if (mapLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(mapLevel));
        }

        return mapLevel switch
        {
            1 => 0,
            2 => 20,
            3 => 25,
            _ => 35,
        };
    }

    public static EliteSelectionResult Select(
        ulong runSeed,
        ulong encounterSeed,
        int mapLevel,
        int waveIndex,
        int spawnOrdinal,
        bool isBoss,
        string mapModifierId,
        IReadOnlyList<EliteModifierDefinition>? candidates = null)
    {
        if (runSeed == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runSeed));
        }

        if (encounterSeed == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(encounterSeed));
        }

        if (mapLevel < 1 || waveIndex < 0 || spawnOrdinal < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mapLevel),
                "Elite spawn identity must have a positive map level and ordinal.");
        }

        if (string.IsNullOrWhiteSpace(mapModifierId))
        {
            throw new ArgumentException("Map modifier id is required.", nameof(mapModifierId));
        }

        var selectionSeed = DeriveSelectionSeed(
            runSeed,
            encounterSeed,
            mapLevel,
            waveIndex,
            spawnOrdinal);
        if (isBoss || EligibilityRatePercent(mapLevel) == 0)
        {
            return new EliteSelectionResult(null, selectionSeed);
        }

        var definitions = (candidates ?? EliteModifierLibrary.All)
            .Where(definition => definition != null)
            .OrderBy(definition => definition.Id, StringComparer.Ordinal)
            .ToArray();
        if (definitions.Length == 0)
        {
            throw new ArgumentException("Elite catalog must contain at least one definition.", nameof(candidates));
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var eligible = new List<EliteModifierDefinition>();
        foreach (var definition in definitions)
        {
            definition.Validate();
            if (!ids.Add(definition.Id))
            {
                throw new ArgumentException(
                    $"Elite modifier id '{definition.Id}' is duplicated.",
                    nameof(candidates));
            }

            if (definition.IsEligible(mapLevel))
            {
                eligible.Add(definition);
            }
        }

        if (eligible.Count == 0)
        {
            return new EliteSelectionResult(null, selectionSeed);
        }

        var random = new RandomService(selectionSeed);
        if (!random.Chance(EligibilityRatePercent(mapLevel)))
        {
            return new EliteSelectionResult(null, selectionSeed);
        }

        var weights = eligible
            .Select(definition => checked(definition.Weight * ModifierWeightMultiplier(mapModifierId, definition.Id)))
            .ToArray();
        var selected = eligible[random.WeightedChoiceIndex(weights)];
        return new EliteSelectionResult(selected.Id, selectionSeed);
    }

    private static int ModifierWeightMultiplier(string mapModifierId, string eliteId)
    {
        return mapModifierId switch
        {
            "hardened-front" when eliteId == "bulwark" => 3,
            "volatile-hunt" when eliteId is "frenzied" or "volcanic" => 3,
            _ => 1,
        };
    }

    private static ulong DeriveSelectionSeed(
        ulong runSeed,
        ulong encounterSeed,
        int mapLevel,
        int waveIndex,
        int spawnOrdinal)
    {
        var seed = RandomService.DeriveSeed(runSeed, EliteStreamNamespace);
        seed = RandomService.DeriveSeed(seed, encounterSeed);
        seed = RandomService.DeriveSeed(
            seed,
            unchecked(((ulong)(uint)mapLevel << 32) | (uint)waveIndex));
        return RandomService.DeriveSeed(seed, (ulong)(uint)spawnOrdinal);
    }
}
