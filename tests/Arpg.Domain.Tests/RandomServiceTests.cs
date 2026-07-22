using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class RandomServiceTests
{
    [Fact]
    public void Same_seed_reproduces_the_integer_sequence()
    {
        var first = new RandomService(123456);
        var second = new RandomService(123456);
        var different = new RandomService(123457);
        var differentSeedChanged = false;

        for (var index = 0; index < 24; index++)
        {
            var firstValue = first.NextInt(-1000, 1000);
            var secondValue = second.NextInt(-1000, 1000);
            var differentValue = different.NextInt(-1000, 1000);

            Assert.Equal(firstValue, secondValue);
            differentSeedChanged |= firstValue != differentValue;
        }

        Assert.True(differentSeedChanged);
    }

    [Fact]
    public void Integer_and_index_boundaries_are_safe()
    {
        var random = new RandomService(9);

        Assert.Equal(7, random.NextInt(7, 7));
        var reversed = random.NextInt(10, 1);
        Assert.InRange(reversed, 1, 10);
        Assert.Equal(4UL, random.NextUInt64(4, 4));
        Assert.Equal(0, random.NextIndex(0));
        Assert.Equal(0, random.NextIndex(-1));
        Assert.InRange(random.NextFloat01(), 0.0, 0.9999999999999999);
        Assert.False(random.Chance(0));
        Assert.True(random.Chance(100));
    }

    [Fact]
    public void Weighted_choice_skips_non_positive_buckets_and_handles_empty_input()
    {
        var random = new RandomService(9);

        Assert.Equal(0, random.WeightedChoiceIndex(Array.Empty<int>()));
        Assert.Equal(0, random.WeightedChoiceIndex(new[] { -1, 0, 0 }));

        var selected = random.WeightedChoiceIndex(new[] { 0, 3, 7 });
        Assert.InRange(selected, 1, 2);
    }

    [Fact]
    public void State_checkpoint_replays_the_same_sequence()
    {
        var random = new RandomService(901);
        _ = random.NextUInt64(0, ulong.MaxValue);
        var checkpoint = random.CaptureState();
        var expected = new[] { random.NextInt(0, 100), random.NextInt(0, 100), random.NextInt(0, 100) };

        random.RestoreState(checkpoint);
        var replayed = new[] { random.NextInt(0, 100), random.NextInt(0, 100), random.NextInt(0, 100) };

        Assert.Equal(expected, replayed);
    }

    [Fact]
    public void Derived_stream_seeds_are_distinct_and_reseeding_restarts_the_stream()
    {
        Assert.NotEqual(RandomService.DeriveSeed(1, 0), RandomService.DeriveSeed(1, 1));

        var random = new RandomService(42);
        var first = random.NextUInt64(0, ulong.MaxValue);
        random.Reseed(42);

        Assert.Equal(first, random.NextUInt64(0, ulong.MaxValue));
    }
}
