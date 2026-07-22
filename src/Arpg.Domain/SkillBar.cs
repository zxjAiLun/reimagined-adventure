namespace Arpg.Domain;

public sealed class SkillBar
{
    private readonly IReadOnlyDictionary<SkillSlot, SkillDefinition> _definitions;

    public SkillBar(params SkillDefinition[] definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (definitions.Length != Enum.GetValues<SkillSlot>().Length)
        {
            throw new ArgumentException("A first-phase skill bar requires one skill per slot.", nameof(definitions));
        }

        var map = new Dictionary<SkillSlot, SkillDefinition>();
        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            definition.Validate();
            if (!map.TryAdd(definition.Slot, definition))
            {
                throw new ArgumentException($"Skill slot {definition.Slot} is assigned more than once.", nameof(definitions));
            }
        }

        foreach (var slot in Enum.GetValues<SkillSlot>())
        {
            if (!map.ContainsKey(slot))
            {
                throw new ArgumentException($"Skill slot {slot} is not assigned.", nameof(definitions));
            }
        }

        _definitions = map;
    }

    public SkillDefinition this[SkillSlot slot] => Get(slot);

    public IReadOnlyCollection<SkillDefinition> Definitions => _definitions.Values.ToArray();

    public SkillDefinition Get(SkillSlot slot)
    {
        if (!_definitions.TryGetValue(slot, out var definition))
        {
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "Skill slot is not assigned.");
        }

        return definition;
    }
}
