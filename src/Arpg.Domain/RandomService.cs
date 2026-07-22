namespace Arpg.Domain;

/// <summary>
/// Explicit, platform-stable seeded randomness for gameplay rules.
/// The state transition is SplitMix64, so the same seed and calls produce the
/// same sequence without depending on a runtime-specific System.Random.
/// </summary>
public sealed class RandomService
{
    public const ulong DefaultSeed = 0x4D494E4941525047UL;

    private const ulong Gamma = 0x9E3779B97F4A7C15UL;
    private const ulong MixA = 0xBF58476D1CE4E5B9UL;
    private const ulong MixB = 0x94D049BB133111EBUL;

    private ulong _state;

    public RandomService(ulong seed = DefaultSeed)
    {
        Seed = seed;
        _state = seed;
    }

    public ulong Seed { get; private set; }

    public ulong State => _state;

    public void Reseed(ulong seed)
    {
        Seed = seed;
        _state = seed;
    }

    public ulong CaptureState() => _state;

    public void RestoreState(ulong state) => _state = state;

    public int NextInt(int minimum, int maximum)
    {
        if (minimum > maximum)
        {
            (minimum, maximum) = (maximum, minimum);
        }

        var range = (ulong)((long)maximum - minimum) + 1UL;
        var offset = NextUInt64(0, range - 1UL);
        return (int)((long)minimum + (long)offset);
    }

    public ulong NextUInt64(ulong minimum, ulong maximum)
    {
        if (minimum > maximum)
        {
            (minimum, maximum) = (maximum, minimum);
        }

        if (minimum == maximum)
        {
            return minimum;
        }

        var range = maximum - minimum + 1UL;
        if (range == 0UL)
        {
            return NextRawUInt64();
        }

        var threshold = unchecked(0UL - range) % range;
        ulong sample;
        do
        {
            sample = NextRawUInt64();
        }
        while (sample < threshold);

        return minimum + sample % range;
    }

    public double NextFloat01()
    {
        // Convert the high 53 bits to [0, 1), independent of runtime details.
        return (NextRawUInt64() >> 11) * (1.0 / (1UL << 53));
    }

    public bool Chance(int percentage)
    {
        if (percentage <= 0)
        {
            return false;
        }

        if (percentage >= 100)
        {
            return true;
        }

        return NextInt(0, 99) < percentage;
    }

    public int NextIndex(int size)
    {
        return size <= 0 ? 0 : NextInt(0, size - 1);
    }

    public int WeightedChoiceIndex(IReadOnlyList<int> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        if (weights.Count == 0)
        {
            return 0;
        }

        ulong total = 0;
        foreach (var weight in weights)
        {
            if (weight <= 0)
            {
                continue;
            }

            var positiveWeight = (ulong)weight;
            if (total > ulong.MaxValue - positiveWeight)
            {
                total = ulong.MaxValue;
                break;
            }

            total += positiveWeight;
        }

        if (total == 0)
        {
            return 0;
        }

        var target = NextUInt64(0, total - 1UL);
        var remaining = target;
        for (var index = 0; index < weights.Count; index++)
        {
            var positiveWeight = (ulong)Math.Max(0, weights[index]);
            if (remaining < positiveWeight)
            {
                return index;
            }

            remaining -= positiveWeight;
        }

        return weights.Count - 1;
    }

    public static ulong DeriveSeed(ulong seed, ulong stream)
    {
        var value = unchecked(seed + Gamma * (stream + 1UL));
        value = unchecked((value ^ (value >> 30)) * MixA);
        value = unchecked((value ^ (value >> 27)) * MixB);
        return value ^ (value >> 31);
    }

    private ulong NextRawUInt64()
    {
        _state = unchecked(_state + Gamma);
        var value = _state;
        value = unchecked((value ^ (value >> 30)) * MixA);
        value = unchecked((value ^ (value >> 27)) * MixB);
        return value ^ (value >> 31);
    }
}
