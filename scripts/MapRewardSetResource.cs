using System.Collections.Generic;
using Arpg.Domain;
using Godot;

[GlobalClass]
public partial class MapRewardSetResource : Resource
{
    [Export] public Godot.Collections.Array<MapRewardResource> Rewards { get; set; } = new();

    public IReadOnlyList<MapRewardDefinition> ToDomain()
    {
        var rewards = new List<MapRewardDefinition>();
        foreach (var resource in Rewards ?? new Godot.Collections.Array<MapRewardResource>())
        {
            if (resource != null)
            {
                rewards.Add(resource.ToDomain());
            }
        }

        return rewards.Count == 0 ? MapRewardLibrary.FallbackRewards : rewards;
    }
}
