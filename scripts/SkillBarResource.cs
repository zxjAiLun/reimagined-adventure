using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// Resource-backed skill bar. This keeps authoring data in Godot while the
/// active runtime bar remains a pure Domain SkillBar.
/// </summary>
[GlobalClass]
public partial class SkillBarResource : Resource
{
    [Export] public SkillDefinitionResource Primary { get; set; }
    [Export] public SkillDefinitionResource Secondary { get; set; }
    [Export] public SkillDefinitionResource Utility { get; set; }
    [Export] public SkillDefinitionResource Movement { get; set; }

    public SkillBar ToDomain()
    {
        return new SkillBar(
            Require(Primary, SkillSlot.Primary),
            Require(Secondary, SkillSlot.Secondary),
            Require(Utility, SkillSlot.Utility),
            Require(Movement, SkillSlot.Movement));
    }

    private static SkillDefinition Require(SkillDefinitionResource resource, SkillSlot slot)
    {
        if (resource == null)
        {
            throw new InvalidOperationException($"Skill resource for {slot} is missing.");
        }

        return resource.ToDomain();
    }
}
