using Godot;

public partial class Milestone15MapModifierSmoke : Node
{
    public override void _Ready()
    {
        var modifier = GetNode<MapModifierNode>("MapModifier");
        if (modifier.Definition.Id != "hardened-front"
            || modifier.EnemyHp <= 1
            || modifier.BossHp <= modifier.EnemyHp
            || modifier.ItemLevel != 3)
        {
            GD.PrintErr("MILESTONE15_MAP_MODIFIER_FAIL");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"MILESTONE15_MAP_MODIFIER_PASS enemy_hp={modifier.EnemyHp} boss_hp={modifier.BossHp} item_level={modifier.ItemLevel}");
        GetTree().Quit(0);
    }
}
