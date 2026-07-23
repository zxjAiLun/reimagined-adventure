namespace Arpg.Domain;

public sealed class AtlasMapDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int Tier { get; init; } = 1;
    public string? PrerequisiteMapId { get; init; }
    public string MapModifierId { get; init; } = "quiet-coast";
    public int ItemLevel { get; init; } = 1;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Atlas map id and name are required.");
        }

        if (Tier < 1 || ItemLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Tier), "Atlas tier and item level must be positive.");
        }

        if (PrerequisiteMapId != null && string.IsNullOrWhiteSpace(PrerequisiteMapId))
        {
            throw new ArgumentException("Prerequisite map id cannot be blank.", nameof(PrerequisiteMapId));
        }

        if (string.IsNullOrWhiteSpace(MapModifierId))
        {
            throw new ArgumentException("Atlas map modifier id is required.", nameof(MapModifierId));
        }

        if (MapModifierLibrary.Find(MapModifierId) == null)
        {
            throw new ArgumentException($"Unknown map modifier '{MapModifierId}'.", nameof(MapModifierId));
        }
    }
}

public sealed class AtlasDefinition
{
    public IReadOnlyList<AtlasMapDefinition> Maps { get; init; } = Array.Empty<AtlasMapDefinition>();

    public AtlasMapDefinition? Find(string? mapId) =>
        string.IsNullOrWhiteSpace(mapId)
            ? null
            : Maps.FirstOrDefault(map => map.Id == mapId);

    public void Validate()
    {
        if (Maps == null || Maps.Count == 0)
        {
            throw new ArgumentException("Atlas must contain at least one map.", nameof(Maps));
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var map in Maps)
        {
            ArgumentNullException.ThrowIfNull(map);
            map.Validate();
            if (!ids.Add(map.Id))
            {
                throw new ArgumentException($"Atlas map id '{map.Id}' is duplicated.", nameof(Maps));
            }

            if (map.PrerequisiteMapId == map.Id)
            {
                throw new ArgumentException($"Atlas map '{map.Id}' cannot require itself.", nameof(Maps));
            }

            if (map.PrerequisiteMapId != null && !Maps.Any(candidate => candidate.Id == map.PrerequisiteMapId))
            {
                throw new ArgumentException($"Atlas prerequisite '{map.PrerequisiteMapId}' does not exist.", nameof(Maps));
            }
        }

        foreach (var map in Maps)
        {
            EnsureAcyclic(map, new HashSet<string>(StringComparer.Ordinal));
        }
    }

    private void EnsureAcyclic(AtlasMapDefinition map, HashSet<string> path)
    {
        if (!path.Add(map.Id))
        {
            throw new ArgumentException($"Atlas prerequisite cycle includes '{map.Id}'.", nameof(Maps));
        }

        if (map.PrerequisiteMapId != null)
        {
            EnsureAcyclic(Find(map.PrerequisiteMapId)!, path);
        }

        path.Remove(map.Id);
    }
}

public sealed class AtlasState
{
    private readonly AtlasDefinition _definition;
    private readonly HashSet<string> _unlocked = new(StringComparer.Ordinal);
    private readonly HashSet<string> _completed = new(StringComparer.Ordinal);

    public AtlasState(AtlasDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        _definition = definition;
        foreach (var map in definition.Maps.Where(map => map.PrerequisiteMapId == null))
        {
            _unlocked.Add(map.Id);
        }
    }

    public IReadOnlyCollection<string> UnlockedMapIds => _unlocked;
    public IReadOnlyCollection<string> CompletedMapIds => _completed;
    public IReadOnlyList<AtlasMapDefinition> AvailableMaps =>
        _definition.Maps.Where(map => _unlocked.Contains(map.Id) && !_completed.Contains(map.Id)).ToArray();

    public bool IsUnlocked(string? mapId) =>
        !string.IsNullOrWhiteSpace(mapId) && _unlocked.Contains(mapId);

    public bool IsCompleted(string? mapId) =>
        !string.IsNullOrWhiteSpace(mapId) && _completed.Contains(mapId);

    public bool TryUnlock(string mapId)
    {
        var map = _definition.Find(mapId);
        if (map == null || _unlocked.Contains(map.Id))
        {
            return false;
        }

        if (map.PrerequisiteMapId != null && !_completed.Contains(map.PrerequisiteMapId))
        {
            return false;
        }

        _unlocked.Add(map.Id);
        return true;
    }

    public bool TryComplete(string mapId)
    {
        if (!IsUnlocked(mapId) || IsCompleted(mapId))
        {
            return false;
        }

        _completed.Add(mapId);
        foreach (var map in _definition.Maps.Where(map => map.PrerequisiteMapId == mapId))
        {
            _unlocked.Add(map.Id);
        }

        return true;
    }

    public bool TryRestore(IEnumerable<string> unlockedMapIds, IEnumerable<string> completedMapIds)
    {
        ArgumentNullException.ThrowIfNull(unlockedMapIds);
        ArgumentNullException.ThrowIfNull(completedMapIds);
        var unlocked = unlockedMapIds.ToHashSet(StringComparer.Ordinal);
        var completed = completedMapIds.ToHashSet(StringComparer.Ordinal);

        if (!CanRestore(unlocked, completed))
        {
            return false;
        }

        _unlocked.Clear();
        _completed.Clear();
        _unlocked.UnionWith(unlocked);
        _completed.UnionWith(completed);
        return true;
    }

    public bool CanRestore(IEnumerable<string> unlockedMapIds, IEnumerable<string> completedMapIds)
    {
        ArgumentNullException.ThrowIfNull(unlockedMapIds);
        ArgumentNullException.ThrowIfNull(completedMapIds);
        var unlocked = unlockedMapIds.ToHashSet(StringComparer.Ordinal);
        var completed = completedMapIds.ToHashSet(StringComparer.Ordinal);

        if (!unlocked.All(id => _definition.Find(id) != null)
            || !completed.All(id => _definition.Find(id) != null)
            || !completed.IsSubsetOf(unlocked))
        {
            return false;
        }

        foreach (var map in _definition.Maps)
        {
            if (map.PrerequisiteMapId == null && !unlocked.Contains(map.Id))
            {
                return false;
            }

            if (completed.Contains(map.Id)
                && map.PrerequisiteMapId != null
                && !completed.Contains(map.PrerequisiteMapId))
            {
                return false;
            }

            if (unlocked.Contains(map.Id)
                && map.PrerequisiteMapId != null
                && !completed.Contains(map.PrerequisiteMapId))
            {
                return false;
            }
        }

        return true;
    }

    public MapModifierStats? MapModifier(string mapId)
    {
        var map = _definition.Find(mapId);
        return map == null ? null : MapModifierLibrary.Find(map.MapModifierId)?.Effects;
    }
}

public static class AtlasLibrary
{
    public static AtlasDefinition MinimumSlice() => new()
    {
        Maps =
        [
            new AtlasMapDefinition
            {
                Id = "quiet-coast",
                Name = "Quiet Coast",
                Tier = 1,
                MapModifierId = "quiet-coast",
                ItemLevel = 1,
            },
            new AtlasMapDefinition
            {
                Id = "hardened-frontier",
                Name = "Hardened Frontier",
                Tier = 2,
                PrerequisiteMapId = "quiet-coast",
                MapModifierId = "hardened-front",
                ItemLevel = 2,
            },
            new AtlasMapDefinition
            {
                Id = "brimstone-caldera",
                Name = "Brimstone Caldera",
                Tier = 3,
                PrerequisiteMapId = "hardened-frontier",
                MapModifierId = "frenzied-march",
                ItemLevel = 3,
            },
        ],
    };
}
