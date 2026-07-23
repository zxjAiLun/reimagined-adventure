using Arpg.Domain;
using Godot;

public partial class Milestone12PassiveTreeSmoke : Node
{
    public override void _Ready()
    {
        var player = GetNode<PlayerController>("Player");
        var tree = GetNode<PassiveTreeNode>("Player/PassiveTree");
        var before = tree.State.CombinedStats();
        var beforeHealth = player.MaxHealth;
        var childBeforeParent = tree.TryAllocate(1);
        var rootAllocated = tree.TryAllocate(0);
        var childAllocated = tree.TryAllocate(1);
        var survivalAllocated = tree.TryAllocate(2);
        var after = tree.State.CombinedStats();

        if (childBeforeParent
            || !rootAllocated
            || !childAllocated
            || !survivalAllocated
            || after.ProjectileDamageMultiplier <= before.ProjectileDamageMultiplier
            || after.AttackSpeedMultiplier <= before.AttackSpeedMultiplier
            || beforeHealth != 100
            || player.MaxHealth != 105)
        {
            GD.PrintErr("MILESTONE12_PASSIVE_TREE_FAIL");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"MILESTONE12_PASSIVE_TREE_PASS projectile={after.ProjectileDamageMultiplier:0.00} attack_speed={after.AttackSpeedMultiplier:0.00} hp={beforeHealth}->{player.MaxHealth}");
        GetTree().Quit(0);
    }
}
