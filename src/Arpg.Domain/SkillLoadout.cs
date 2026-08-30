namespace Arpg.Domain;

/// <summary>
/// Run-owned support configuration. Support identity and compatibility are
/// validated here; Godot only presents and consumes the current result.
/// </summary>
public sealed class SkillLoadout
{
    public static IReadOnlyList<string> DefaultUnlockedSupportIds =>
        ["volley", "amplify", "frostbite", "combustion", "overload"];

    private readonly SkillBar _skillBar;
    private readonly Dictionary<SkillSlot, string> _supportIds = new();
    private readonly HashSet<string> _unlockedSupportIds = new(StringComparer.Ordinal);

    public SkillLoadout(
        SkillBar skillBar,
        IEnumerable<string>? unlockedSupportIds = null,
        IReadOnlyDictionary<SkillSlot, string>? initialSupportIds = null)
    {
        _skillBar = skillBar ?? throw new ArgumentNullException(nameof(skillBar));
        foreach (var supportId in unlockedSupportIds ?? DefaultUnlockedSupportIds)
        {
            if (!TryUnlock(supportId))
            {
                throw new ArgumentException($"Unknown support '{supportId}'.", nameof(unlockedSupportIds));
            }
        }

        if (initialSupportIds != null)
        {
            foreach (var pair in initialSupportIds)
            {
                if (!TryAttach(pair.Key, pair.Value))
                {
                    throw new ArgumentException($"Invalid initial support '{pair.Value}' for {pair.Key}.", nameof(initialSupportIds));
                }
            }
        }
    }

    public event Action? Changed;

    public IReadOnlySet<string> UnlockedSupportIds => _unlockedSupportIds;

    public IReadOnlyDictionary<SkillSlot, string> SupportIdBySkillSlot => _supportIds;

    public SupportDefinition? SupportFor(SkillSlot slot) =>
        _supportIds.TryGetValue(slot, out var supportId)
            ? SupportLibrary.Find(supportId)
            : null;

    public IReadOnlyList<SupportDefinition> SupportsFor(SkillSlot slot)
    {
        var support = SupportFor(slot);
        return support == null ? Array.Empty<SupportDefinition>() : [support];
    }

    public bool IsUnlocked(string supportId) =>
        !string.IsNullOrWhiteSpace(supportId) && _unlockedSupportIds.Contains(supportId);

    public bool TryUnlock(string supportId)
    {
        var support = SupportLibrary.Find(supportId);
        if (support == null)
        {
            return false;
        }

        support.Validate();
        return _unlockedSupportIds.Add(support.Id);
    }

    public bool TryAttach(SkillSlot slot, string supportId)
    {
        if (!Enum.IsDefined(slot)
            || string.IsNullOrWhiteSpace(supportId)
            || !_unlockedSupportIds.Contains(supportId)
            || _supportIds.Any(pair => pair.Key != slot && pair.Value == supportId))
        {
            return false;
        }

        var support = SupportLibrary.Find(supportId);
        if (support == null || !SupportLibrary.SupportsSkill(support, _skillBar[slot]))
        {
            return false;
        }

        if (_supportIds.TryGetValue(slot, out var current) && current == supportId)
        {
            return true;
        }

        _supportIds[slot] = supportId;
        Changed?.Invoke();
        return true;
    }

    public bool TryDetach(SkillSlot slot)
    {
        if (!_supportIds.Remove(slot))
        {
            return false;
        }

        Changed?.Invoke();
        return true;
    }

    public void Restore(
        IEnumerable<string> unlockedSupportIds,
        IReadOnlyDictionary<SkillSlot, string> supportIdBySkillSlot)
    {
        ArgumentNullException.ThrowIfNull(unlockedSupportIds);
        ArgumentNullException.ThrowIfNull(supportIdBySkillSlot);
        var targetUnlocked = unlockedSupportIds.ToArray();
        var targetSupportIds = supportIdBySkillSlot.ToDictionary(pair => pair.Key, pair => pair.Value);

        var candidate = new SkillLoadout(_skillBar, targetUnlocked);
        foreach (var pair in targetSupportIds)
        {
            if (!candidate.TryAttach(pair.Key, pair.Value))
            {
                throw new ArgumentException("Saved support loadout is incompatible or duplicated.", nameof(supportIdBySkillSlot));
            }
        }

        _unlockedSupportIds.Clear();
        foreach (var supportId in candidate._unlockedSupportIds)
        {
            _unlockedSupportIds.Add(supportId);
        }

        _supportIds.Clear();
        foreach (var pair in candidate._supportIds)
        {
            _supportIds[pair.Key] = pair.Value;
        }

        Changed?.Invoke();
    }
}
