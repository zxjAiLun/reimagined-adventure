using System.Collections.Generic;
using Arpg.Domain;
using Godot;

[GlobalClass]
public partial class AtlasDefinitionResource : Resource
{
    [Export] public Godot.Collections.Array<AtlasMapResource> Maps { get; set; } = new();

    public AtlasDefinition ToDomain()
    {
        var maps = new List<AtlasMapDefinition>();
        foreach (var mapResource in Maps ?? new Godot.Collections.Array<AtlasMapResource>())
        {
            if (mapResource != null)
            {
                maps.Add(mapResource.ToDomain());
            }
        }

        if (maps.Count == 0)
        {
            return AtlasLibrary.MinimumSlice();
        }

        var definition = new AtlasDefinition { Maps = maps };
        definition.Validate();
        return definition;
    }
}
