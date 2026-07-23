using Arpg.Domain;
using Godot;

[GlobalClass]
public partial class AtlasMapResource : Resource
{
    [Export] public string MapId { get; set; } = "quiet-coast";
    [Export] public string DisplayName { get; set; } = "Quiet Coast";
    [Export] public int Tier { get; set; } = 1;
    [Export] public string PrerequisiteMapId { get; set; } = string.Empty;
    [Export] public string MapModifierId { get; set; } = "quiet-coast";
    [Export] public int ItemLevel { get; set; } = 1;

    public AtlasMapDefinition ToDomain()
    {
        var map = new AtlasMapDefinition
        {
            Id = MapId,
            Name = DisplayName,
            Tier = Tier,
            PrerequisiteMapId = string.IsNullOrWhiteSpace(PrerequisiteMapId) ? null : PrerequisiteMapId,
            MapModifierId = MapModifierId,
            ItemLevel = ItemLevel,
        };
        map.Validate();
        return map;
    }
}
