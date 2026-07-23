using Godot;

public partial class Milestone8MapModifierSmoke : Node
{
    [Export] public MapModifierResource DefinitionResource { get; set; }

    public override void _Ready()
    {
        var resource = DefinitionResource ?? GD.Load<MapModifierResource>("res://resources/HardenedFront.tres");
        var definition = resource?.ToDomain();
        if (definition == null
            || definition.Id != "hardened-front"
            || definition.Effects.MonsterHpMultiplier <= 1.0)
        {
            GD.PrintErr("MILESTONE8_MAP_MODIFIER_FAIL");
            GetTree().Quit(1);
            return;
        }

        GD.Print("MILESTONE8_MAP_MODIFIER_PASS");
        GetTree().Quit(0);
    }
}
