using Godot;

public partial class Milestone17MapModifierRuntimeSmoke : Node2D
{
    private MapModifierNode _modifier;
    private FeralController _feral;
    private SpitterController _spitter;
    private BrimstoneColossusController _boss;

    public override void _Ready()
    {
        _modifier = GetNode<MapModifierNode>("MapModifier");
        _feral = GetNode<FeralController>("Feral");
        _spitter = GetNode<SpitterController>("Spitter");
        _boss = GetNode<BrimstoneColossusController>("BrimstoneColossus");
        GetTree().CreateTimer(0.05).Timeout += CheckAppliedValues;
    }

    private void CheckAppliedValues()
    {
        var allDropsHaveScaledLevel = true;
        foreach (var node in GetTree().GetNodesInGroup("enemy_loot_droppers"))
        {
            if (node is EnemyLootDropper dropper && dropper.ItemLevel != 3)
            {
                allDropsHaveScaledLevel = false;
            }
        }

        if (_modifier.Definition.Id != "hardened-front"
            || _modifier.EnemyHp != 2
            || _feral.MaxHealth != 69
            || _spitter.MaxHealth != 49
            || _boss.MaxHealth != 290
            || !allDropsHaveScaledLevel)
        {
            GD.PrintErr($"MILESTONE17_MAP_MODIFIER_FAIL feral={_feral.MaxHealth} spitter={_spitter.MaxHealth} boss={_boss.MaxHealth}");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"MILESTONE17_MAP_MODIFIER_PASS feral_hp={_feral.MaxHealth} boss_hp={_boss.MaxHealth} item_level=3");
        GetTree().Quit(0);
    }
}
