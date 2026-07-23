using System.Collections.Generic;
using Arpg.Domain;
using Godot;

[GlobalClass]
public partial class StashResource : Resource
{
    [Export] public Godot.Collections.Array<StashTabResource> Tabs { get; set; } = new();

    public Stash ToDomain()
    {
        var tabs = new List<StashTab>();
        foreach (var tabResource in Tabs ?? new Godot.Collections.Array<StashTabResource>())
        {
            if (tabResource != null)
            {
                tabs.Add(tabResource.ToDomain());
            }
        }

        return tabs.Count == 0 ? Stash.CreateDefault() : new Stash(tabs);
    }
}
