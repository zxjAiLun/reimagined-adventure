using System.Collections.Generic;
using Arpg.Domain;
using Godot;

/// <summary>
/// Resource-backed passive tree definition. Allocation and prerequisite
/// rules stay in PassiveTreeState.
/// </summary>
[GlobalClass]
public partial class PassiveTreeResource : Resource
{
    [Export] public Godot.Collections.Array<PassiveNodeResource> Nodes { get; set; } = new();

    public PassiveTreeDefinition ToDomain()
    {
        var nodes = new List<PassiveNodeDefinition>();
        foreach (var resource in Nodes ?? new Godot.Collections.Array<PassiveNodeResource>())
        {
            if (resource != null)
            {
                nodes.Add(resource.ToDomain());
            }
        }

        if (nodes.Count == 0)
        {
            return PassiveTreeLibrary.MinimumSlice();
        }

        var definition = new PassiveTreeDefinition { Nodes = nodes };
        definition.Validate();
        return definition;
    }
}
