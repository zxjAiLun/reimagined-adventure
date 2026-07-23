using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class MapRewardTests
{
    [Fact]
    public void FallbackDamageRewardChangesStats()
    {
        var reward = MapRewardLibrary.FallbackRewards[0];
        var result = reward.Apply(Stats.Neutral);

        Assert.Equal(1.20, result.DamageMultiplier);
    }

    [Fact]
    public void FallbackRewardsHaveUniqueValidIds()
    {
        var rewards = MapRewardLibrary.FallbackRewards;

        Assert.Equal(rewards.Count, rewards.Select(reward => reward.Id).Distinct().Count());
        foreach (var reward in rewards)
        {
            reward.Validate();
        }
    }
}
