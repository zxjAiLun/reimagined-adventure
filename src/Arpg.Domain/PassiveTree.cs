namespace Arpg.Domain;

public enum PassiveBranch
{
    Projectile,
    Survival,
}

public sealed class PassiveNodeDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public PassiveBranch Branch { get; init; }
    public int PrerequisiteIndex { get; init; } = -1;
    public Stats Stats { get; init; } = Stats.Neutral;

    public void Validate(int nodeCount)
    {
        if (string.IsNullOrWhiteSpace(Id)
            || string.IsNullOrWhiteSpace(Name)
            || string.IsNullOrWhiteSpace(Description))
        {
            throw new ArgumentException("Passive node identity and description are required.");
        }

        if (!Enum.IsDefined(Branch)
            || PrerequisiteIndex < -1
            || PrerequisiteIndex >= nodeCount
            || PrerequisiteIndex == -1 && nodeCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PrerequisiteIndex));
        }

        ArgumentNullException.ThrowIfNull(Stats);
        Stats.Validate();
    }
}

public sealed class PassiveTreeDefinition
{
    public IReadOnlyList<PassiveNodeDefinition> Nodes { get; init; } = Array.Empty<PassiveNodeDefinition>();

    public void Validate()
    {
        if (Nodes == null || Nodes.Count == 0)
        {
            throw new ArgumentException("Passive tree must contain nodes.", nameof(Nodes));
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < Nodes.Count; index++)
        {
            var node = Nodes[index] ?? throw new ArgumentException("Passive tree contains a null node.", nameof(Nodes));
            node.Validate(Nodes.Count);
            if (!ids.Add(node.Id))
            {
                throw new ArgumentException($"Passive node id '{node.Id}' is duplicated.", nameof(Nodes));
            }
        }
    }
}

public sealed class PassiveTreeState
{
    private readonly PassiveTreeDefinition _definition;
    private readonly bool[] _allocated;

    public PassiveTreeState(PassiveTreeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        _definition = definition;
        _allocated = new bool[definition.Nodes.Count];
    }

    public IReadOnlyList<PassiveNodeDefinition> Nodes => _definition.Nodes;

    public IReadOnlyList<int> AllocatedIndices =>
        Enumerable.Range(0, _allocated.Length)
            .Where(index => _allocated[index])
            .ToArray();

    public bool IsAllocated(int index)
    {
        return index >= 0 && index < _allocated.Length && _allocated[index];
    }

    public bool TryAllocate(int index)
    {
        if (index < 0 || index >= _allocated.Length || _allocated[index])
        {
            return false;
        }

        var prerequisite = _definition.Nodes[index].PrerequisiteIndex;
        if (prerequisite >= 0 && !_allocated[prerequisite])
        {
            return false;
        }

        _allocated[index] = true;
        return true;
    }

    public bool TryRestore(IEnumerable<int> indices)
    {
        ArgumentNullException.ThrowIfNull(indices);
        var requested = indices.ToArray();
        if (!CanRestore(requested))
        {
            return false;
        }

        var next = new bool[_allocated.Length];
        foreach (var index in requested)
        {
            next[index] = true;
        }

        Array.Copy(next, _allocated, next.Length);
        return true;
    }

    public bool CanRestore(IEnumerable<int> indices)
    {
        ArgumentNullException.ThrowIfNull(indices);
        var requested = indices.ToArray();
        var next = new bool[_allocated.Length];
        foreach (var index in requested)
        {
            if (index < 0 || index >= next.Length || next[index])
            {
                return false;
            }

            next[index] = true;
        }

        for (var index = 0; index < next.Length; index++)
        {
            var prerequisite = _definition.Nodes[index].PrerequisiteIndex;
            if (next[index] && prerequisite >= 0 && !next[prerequisite])
            {
                return false;
            }
        }

        return true;
    }

    public Stats CombinedStats()
    {
        var result = Stats.Neutral;
        for (var index = 0; index < _allocated.Length; index++)
        {
            if (_allocated[index])
            {
                result = Stats.Combine(result, _definition.Nodes[index].Stats);
            }
        }

        return result;
    }
}

public static class PassiveTreeLibrary
{
    public static PassiveTreeDefinition MinimumSlice() => new()
    {
        Nodes =
        [
            new PassiveNodeDefinition
            {
                Id = "sharpened-bolt",
                Name = "Sharpened Bolt",
                Description = "+8% projectile damage",
                Branch = PassiveBranch.Projectile,
                Stats = new Stats { ProjectileDamageMultiplier = 1.08 },
            },
            new PassiveNodeDefinition
            {
                Id = "rapid-fire",
                Name = "Rapid Fire",
                Description = "+6% attack speed",
                Branch = PassiveBranch.Projectile,
                PrerequisiteIndex = 0,
                Stats = new Stats { AttackSpeedMultiplier = 1.06 },
            },
            new PassiveNodeDefinition
            {
                Id = "vigour",
                Name = "Vigour",
                Description = "+5 max HP",
                Branch = PassiveBranch.Survival,
                Stats = new Stats { MaxHp = 5 },
            },
        ],
    };
}
