using Arpg.Domain;
using Godot;

[GlobalClass]
public partial class StashTabResource : Resource
{
    [Export] public string TabId { get; set; } = "default";
    [Export] public string DisplayName { get; set; } = "Default";
    [Export] public int Capacity { get; set; } = 24;

    public StashTab ToDomain()
    {
        var tab = new StashTab
        {
            Id = TabId,
            Name = DisplayName,
            Capacity = Capacity,
        };
        tab.Validate();
        return tab;
    }
}
