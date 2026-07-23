using Arpg.Domain;
using Godot;

/// <summary>
/// Godot authoring data for one passive node. The runtime definition remains
/// a validated pure Domain object.
/// </summary>
[GlobalClass]
public partial class PassiveNodeResource : Resource
{
    [Export] public string NodeId { get; set; } = "passive";
    [Export] public string DisplayName { get; set; } = "Passive";
    [Export] public string Description { get; set; } = string.Empty;
    [Export] public int Branch { get; set; }
    [Export] public int PrerequisiteIndex { get; set; } = -1;
    [Export] public int MaxHp { get; set; }
    [Export] public float AttackSpeedMultiplier { get; set; } = 1.0f;
    [Export] public float ProjectileDamageMultiplier { get; set; } = 1.0f;

    public PassiveNodeDefinition ToDomain()
    {
        var node = new PassiveNodeDefinition
        {
            Id = NodeId,
            Name = DisplayName,
            Description = Description,
            Branch = (PassiveBranch)Branch,
            PrerequisiteIndex = PrerequisiteIndex,
            Stats = new Stats
            {
                MaxHp = MaxHp,
                AttackSpeedMultiplier = AttackSpeedMultiplier,
                ProjectileDamageMultiplier = ProjectileDamageMultiplier,
            },
        };
        return node;
    }
}
