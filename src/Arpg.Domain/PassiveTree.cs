namespace Arpg.Domain;

public enum PassiveBranch
{
    Projectile,
    Area,
    Elemental,
    Survival,
}

public sealed class PassiveNodeDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public PassiveBranch Branch { get; init; }
    public IReadOnlyList<string> PrerequisiteIds { get; init; } = Array.Empty<string>();
    public int PointCost { get; init; } = 1;

    // Kept only as a source-compatible read path for the original 2D
    // resource. New definitions use PrerequisiteIds exclusively.
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
            || PointCost <= 0
            || PrerequisiteIndex < -1
            || PrerequisiteIndex >= nodeCount)
        {
            throw new ArgumentOutOfRangeException(nameof(PointCost), "Passive node metadata is invalid.");
        }

        if (PrerequisiteIds == null)
        {
            throw new ArgumentException("Passive prerequisite IDs cannot be null.", nameof(PrerequisiteIds));
        }

        if (PrerequisiteIds.Any(string.IsNullOrWhiteSpace)
            || PrerequisiteIds.Distinct(StringComparer.Ordinal).Count() != PrerequisiteIds.Count)
        {
            throw new ArgumentException("Passive prerequisite IDs must be unique and non-empty.", nameof(PrerequisiteIds));
        }

        if (PrerequisiteIds.Count > 0 && PrerequisiteIndex != -1)
        {
            throw new ArgumentException("A passive node cannot mix ID and index prerequisites.");
        }

        ArgumentNullException.ThrowIfNull(Stats);
        Stats.Validate();
    }
}

public sealed class PassiveTreeDefinition
{
    private Dictionary<string, string[]> _normalizedPrerequisites = new(StringComparer.Ordinal);

    public IReadOnlyList<PassiveNodeDefinition> Nodes { get; init; } = Array.Empty<PassiveNodeDefinition>();

    public void Validate()
    {
        if (Nodes == null || Nodes.Count == 0)
        {
            throw new ArgumentException("Passive tree must contain nodes.", nameof(Nodes));
        }

        var nodesById = new Dictionary<string, PassiveNodeDefinition>(StringComparer.Ordinal);
        for (var index = 0; index < Nodes.Count; index++)
        {
            var node = Nodes[index] ?? throw new ArgumentException("Passive tree contains a null node.", nameof(Nodes));
            node.Validate(Nodes.Count);
            if (!nodesById.TryAdd(node.Id, node))
            {
                throw new ArgumentException($"Passive node id '{node.Id}' is duplicated.", nameof(Nodes));
            }
        }

        var prerequisites = new Dictionary<string, string[]>(StringComparer.Ordinal);
        for (var index = 0; index < Nodes.Count; index++)
        {
            var node = Nodes[index];
            var ids = node.PrerequisiteIds.Count > 0
                ? node.PrerequisiteIds.ToArray()
                : node.PrerequisiteIndex >= 0
                    ? [Nodes[node.PrerequisiteIndex].Id]
                    : Array.Empty<string>();

            foreach (var prerequisiteId in ids)
            {
                if (!nodesById.ContainsKey(prerequisiteId))
                {
                    throw new ArgumentException(
                        $"Passive node '{node.Id}' references unknown prerequisite '{prerequisiteId}'.",
                        nameof(Nodes));
                }

                if (string.Equals(node.Id, prerequisiteId, StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Passive node '{node.Id}' cannot require itself.", nameof(Nodes));
                }
            }

            prerequisites[node.Id] = ids;
        }

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in Nodes)
        {
            Visit(node.Id);
        }

        _normalizedPrerequisites = prerequisites;

        void Visit(string nodeId)
        {
            if (visited.Contains(nodeId))
            {
                return;
            }

            if (!visiting.Add(nodeId))
            {
                throw new ArgumentException("Passive tree prerequisites cannot contain cycles.", nameof(Nodes));
            }

            foreach (var prerequisiteId in prerequisites[nodeId])
            {
                Visit(prerequisiteId);
            }

            visiting.Remove(nodeId);
            visited.Add(nodeId);
        }
    }

    public PassiveNodeDefinition? Find(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        return Nodes.FirstOrDefault(node => string.Equals(node.Id, nodeId, StringComparison.Ordinal));
    }

    public IReadOnlyList<string> PrerequisitesFor(string nodeId)
    {
        if (!_normalizedPrerequisites.TryGetValue(nodeId, out var prerequisites))
        {
            throw new ArgumentException($"Unknown passive node '{nodeId}'.", nameof(nodeId));
        }

        return prerequisites;
    }
}

public sealed class PassiveTreeState
{
    private readonly PassiveTreeDefinition _definition;
    private readonly HashSet<string> _allocatedNodeIds = new(StringComparer.Ordinal);

    public PassiveTreeState(PassiveTreeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        _definition = definition;
    }

    public IReadOnlyList<PassiveNodeDefinition> Nodes => _definition.Nodes;

    public IReadOnlySet<string> AllocatedNodeIds => _allocatedNodeIds;

    public IReadOnlyList<int> AllocatedIndices =>
        Enumerable.Range(0, _definition.Nodes.Count)
            .Where(index => _allocatedNodeIds.Contains(_definition.Nodes[index].Id))
            .ToArray();

    public bool IsAllocated(string nodeId) =>
        !string.IsNullOrWhiteSpace(nodeId) && _allocatedNodeIds.Contains(nodeId);

    public bool IsAllocated(int index) =>
        index >= 0
        && index < _definition.Nodes.Count
        && IsAllocated(_definition.Nodes[index].Id);

    public bool CanAllocate(string nodeId, int availablePoints)
    {
        var node = _definition.Find(nodeId);
        if (node == null
            || _allocatedNodeIds.Contains(node.Id)
            || availablePoints < node.PointCost)
        {
            return false;
        }

        return _definition.PrerequisitesFor(node.Id).All(_allocatedNodeIds.Contains);
    }

    public bool TryAllocate(string nodeId, CharacterProgressionState progression)
    {
        ArgumentNullException.ThrowIfNull(progression);
        var node = _definition.Find(nodeId);
        if (node == null || !CanAllocate(node.Id, progression.UnspentPassivePoints))
        {
            return false;
        }

        // CanAllocate proves both the prerequisite and point conditions. The
        // state update is then a single commit: points and node ownership move
        // together, with no observable half-applied result.
        if (!progression.TrySpendPassivePoints(node.PointCost))
        {
            return false;
        }

        _allocatedNodeIds.Add(node.Id);
        return true;
    }

    public bool TryAllocate(int index)
    {
        if (index < 0 || index >= _definition.Nodes.Count)
        {
            return false;
        }

        var node = _definition.Nodes[index];
        if (!CanAllocate(node.Id, int.MaxValue))
        {
            return false;
        }

        _allocatedNodeIds.Add(node.Id);
        return true;
    }

    public bool CanRestore(IEnumerable<string> nodeIds, int spentPassivePoints)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        if (spentPassivePoints < 0)
        {
            return false;
        }

        var requested = nodeIds.ToArray();
        if (requested.Any(string.IsNullOrWhiteSpace)
            || requested.Distinct(StringComparer.Ordinal).Count() != requested.Length)
        {
            return false;
        }

        var requestedSet = requested.ToHashSet(StringComparer.Ordinal);
        var pointCost = 0;
        foreach (var nodeId in requested)
        {
            var node = _definition.Find(nodeId);
            if (node == null
                || _definition.PrerequisitesFor(nodeId).Any(prerequisite => !requestedSet.Contains(prerequisite)))
            {
                return false;
            }

            pointCost = checked(pointCost + node.PointCost);
        }

        return pointCost == spentPassivePoints;
    }

    public bool TryRestore(IEnumerable<string> nodeIds, int spentPassivePoints)
    {
        if (!CanRestore(nodeIds, spentPassivePoints))
        {
            return false;
        }

        _allocatedNodeIds.Clear();
        foreach (var nodeId in nodeIds)
        {
            _allocatedNodeIds.Add(nodeId);
        }

        return true;
    }

    public void Restore(IEnumerable<string> nodeIds, int spentPassivePoints)
    {
        if (!TryRestore(nodeIds, spentPassivePoints))
        {
            throw new ArgumentException("Saved passive allocation is invalid.", nameof(nodeIds));
        }
    }

    public bool CanRestore(IEnumerable<int> indices)
    {
        ArgumentNullException.ThrowIfNull(indices);
        var requested = indices.ToArray();
        if (!TryMapIndices(requested, out var nodeIds))
        {
            return false;
        }

        return CanRestore(nodeIds, TotalPointCost(nodeIds));
    }

    public bool TryRestore(IEnumerable<int> indices)
    {
        ArgumentNullException.ThrowIfNull(indices);
        var requested = indices.ToArray();
        return TryMapIndices(requested, out var nodeIds)
            && TryRestore(nodeIds, TotalPointCost(nodeIds));
    }

    public Stats CombinedStats()
    {
        var result = Stats.Neutral;
        foreach (var node in _definition.Nodes)
        {
            if (_allocatedNodeIds.Contains(node.Id))
            {
                result = Stats.Combine(result, node.Stats);
            }
        }

        return result;
    }

    private int TotalPointCost(IEnumerable<string> nodeIds) =>
        nodeIds.Sum(nodeId => _definition.Find(nodeId)?.PointCost ?? 0);

    private bool TryMapIndices(IReadOnlyList<int> indices, out string[] nodeIds)
    {
        nodeIds = Array.Empty<string>();
        if (indices.Any(index => index < 0 || index >= _definition.Nodes.Count)
            || indices.Distinct().Count() != indices.Count)
        {
            return false;
        }

        nodeIds = indices.Select(index => _definition.Nodes[index].Id).ToArray();
        return true;
    }
}

public static class PassiveTreeLibrary
{
    public static IReadOnlyDictionary<int, string> LegacyIndexToNodeId { get; } =
        new Dictionary<int, string>
        {
            [0] = "sharpened-bolt",
            [1] = "rapid-fire",
            [2] = "vigour",
        };

    public static bool TryMapLegacyIndices(IEnumerable<int> indices, out IReadOnlyList<string> nodeIds)
    {
        ArgumentNullException.ThrowIfNull(indices);
        var mapped = new List<string>();
        foreach (var index in indices)
        {
            if (!LegacyIndexToNodeId.TryGetValue(index, out var nodeId)
                || mapped.Contains(nodeId, StringComparer.Ordinal))
            {
                nodeIds = Array.Empty<string>();
                return false;
            }

            mapped.Add(nodeId);
        }

        nodeIds = mapped;
        return true;
    }

    public static bool TryValidateStableAllocation(
        IEnumerable<string> nodeIds,
        int totalExperience,
        out int spentPassivePoints)
    {
        spentPassivePoints = 0;
        if (totalExperience < 0)
        {
            return false;
        }

        var state = new PassiveTreeState(MasterySlice());
        var requested = nodeIds?.ToArray() ?? Array.Empty<string>();
        spentPassivePoints = requested.Sum(nodeId => state.Nodes.FirstOrDefault(node => node.Id == nodeId)?.PointCost ?? 0);
        var progression = new CharacterProgressionState(totalExperience);
        return state.CanRestore(requested, spentPassivePoints)
            && spentPassivePoints <= progression.TotalPassivePointsEarned;
    }

    public static bool TryMigrateLegacyIndices(
        IEnumerable<int> indices,
        int totalExperience,
        out IReadOnlyList<string> nodeIds,
        out int spentPassivePoints)
    {
        spentPassivePoints = 0;
        if (!TryMapLegacyIndices(indices, out nodeIds)
            || !TryValidateStableAllocation(nodeIds, totalExperience, out spentPassivePoints))
        {
            nodeIds = Array.Empty<string>();
            spentPassivePoints = 0;
            return false;
        }

        return true;
    }

    // Compatibility definition used by the existing 2D boundary.
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
                PrerequisiteIds = ["sharpened-bolt"],
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

    public static PassiveTreeDefinition MasterySlice() => new()
    {
        Nodes =
        [
            new PassiveNodeDefinition
            {
                Id = "sharpened-bolt",
                Name = "Sharpened Bolt",
                Description = "+10% Projectile Damage",
                Branch = PassiveBranch.Projectile,
                Stats = new Stats { ProjectileDamageMultiplier = 1.10 },
            },
            new PassiveNodeDefinition
            {
                Id = "rapid-fire",
                Name = "Rapid Fire",
                Description = "+8% Attack Speed",
                Branch = PassiveBranch.Projectile,
                PrerequisiteIds = ["sharpened-bolt"],
                Stats = new Stats { AttackSpeedMultiplier = 1.08 },
            },
            new PassiveNodeDefinition
            {
                Id = "split-focus",
                Name = "Split Focus",
                Description = "+1 Projectile, -10% Projectile Damage",
                Branch = PassiveBranch.Projectile,
                PrerequisiteIds = ["rapid-fire"],
                Stats = new Stats { ProjectileCountBonus = 1, ProjectileDamageMultiplier = 0.90 },
            },
            new PassiveNodeDefinition
            {
                Id = "wide-impact",
                Name = "Wide Impact",
                Description = "+12% Area Radius",
                Branch = PassiveBranch.Area,
                Stats = new Stats { AreaRadiusMultiplier = 1.12 },
            },
            new PassiveNodeDefinition
            {
                Id = "resonance",
                Name = "Resonance",
                Description = "+12% Area Damage",
                Branch = PassiveBranch.Area,
                PrerequisiteIds = ["wide-impact"],
                Stats = new Stats { AreaDamageMultiplier = 1.12 },
            },
            new PassiveNodeDefinition
            {
                Id = "quickened-ritual",
                Name = "Quickened Ritual",
                Description = "+10% Cooldown Recovery",
                Branch = PassiveBranch.Area,
                PrerequisiteIds = ["resonance"],
                Stats = new Stats { CooldownRecoveryMultiplier = 1.10 },
            },
            new PassiveNodeDefinition
            {
                Id = "kindling",
                Name = "Kindling",
                Description = "+12% Fire Damage",
                Branch = PassiveBranch.Elemental,
                Stats = new Stats { FireDamageMultiplier = 1.12 },
            },
            new PassiveNodeDefinition
            {
                Id = "overcharge",
                Name = "Overcharge",
                Description = "+12% Lightning Damage",
                Branch = PassiveBranch.Elemental,
                PrerequisiteIds = ["kindling"],
                Stats = new Stats { LightningDamageMultiplier = 1.12 },
            },
            new PassiveNodeDefinition
            {
                Id = "unstable-elements",
                Name = "Unstable Elements",
                Description = "+15% Ailment Chance, +20% Ailment Duration",
                Branch = PassiveBranch.Elemental,
                PrerequisiteIds = ["overcharge"],
                Stats = new Stats { AilmentChanceBonus = 15, AilmentDurationMultiplier = 1.20 },
            },
            new PassiveNodeDefinition
            {
                Id = "vigour",
                Name = "Vigour",
                Description = "+15 Max HP",
                Branch = PassiveBranch.Survival,
                Stats = new Stats { MaxHp = 15 },
            },
            new PassiveNodeDefinition
            {
                Id = "iron-skin",
                Name = "Iron Skin",
                Description = "+10 Armor",
                Branch = PassiveBranch.Survival,
                PrerequisiteIds = ["vigour"],
                Stats = new Stats { Armor = 10 },
            },
            new PassiveNodeDefinition
            {
                Id = "elemental-ward",
                Name = "Elemental Ward",
                Description = "+8 Fire/Cold/Lightning Resistance, -4% Incoming Damage",
                Branch = PassiveBranch.Survival,
                PrerequisiteIds = ["iron-skin"],
                Stats = new Stats
                {
                    FireResistance = 8,
                    ColdResistance = 8,
                    LightningResistance = 8,
                    IncomingDamageMultiplier = 0.96,
                },
            },
        ],
    };

    public static PassiveTreeDefinition Default() => MasterySlice();
}
