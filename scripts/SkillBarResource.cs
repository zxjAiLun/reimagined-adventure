using System;
using System.Collections.Generic;
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

    public IReadOnlyList<SupportDefinition> SupportsFor(SkillSlot slot)
    {
        return ResourceFor(slot)?.ToSupports() ?? Array.Empty<SupportDefinition>();
    }

    private SkillDefinitionResource ResourceFor(SkillSlot slot)
    {
        return slot switch
        {
            SkillSlot.Primary => Primary,
            SkillSlot.Secondary => Secondary,
            SkillSlot.Utility => Utility,
            SkillSlot.Movement => Movement,
            _ => null,
        };
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
