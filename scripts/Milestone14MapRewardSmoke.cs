using Godot;

public partial class Milestone14MapRewardSmoke : Node
{
    public override void _Ready()
    {
        var player = GetNode<PlayerController>("Player");
        var rewards = GetNode<MapRewardNode>("MapRewards");
        var beforeDamage = player.EffectiveStats.DamageMultiplier;

        var chosen = rewards.TryChooseReward(0);
        var afterDamage = player.EffectiveStats.DamageMultiplier;
        var secondChoice = rewards.TryChooseReward(1);

        if (!chosen || secondChoice || afterDamage <= beforeDamage || rewards.ChosenReward?.Id != "global_damage")
        {
            GD.PrintErr("MILESTONE14_MAP_REWARD_FAIL");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"MILESTONE14_MAP_REWARD_PASS damage={beforeDamage:0.00}->{afterDamage:0.00}");
        GetTree().Quit(0);
    }
}
