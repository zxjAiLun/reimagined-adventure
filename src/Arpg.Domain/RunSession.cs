namespace Arpg.Domain;

/// <summary>
/// Run-scoped deterministic state. Random streams are separated by concern,
/// while all item-producing systems share the same identity sequence.
/// </summary>
public sealed class RunSession : IItemIdSource
{
    public RunSession(ulong runSeed = RandomService.DefaultSeed, int mapLevel = 1)
    {
        if (mapLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(mapLevel), "Map level must be positive.");
        }

        RunSeed = runSeed;
        MapLevel = mapLevel;
        LootRandom = new RandomService(RandomService.DeriveSeed(runSeed, 1));
        CraftingRandom = new RandomService(RandomService.DeriveSeed(runSeed, 2));
        EventRandom = new RandomService(RandomService.DeriveSeed(runSeed, 3));
    }

    public ulong RunSeed { get; private set; }
    public int ItemSequence { get; private set; }
    public int MapLevel { get; private set; }
    public RandomService LootRandom { get; }
    public RandomService CraftingRandom { get; }
    public RandomService EventRandom { get; }

    public string NextId()
    {
        if (ItemSequence == int.MaxValue)
        {
            throw new InvalidOperationException("Run item identity sequence is exhausted.");
        }

        ItemSequence++;
        return $"item_{ItemSequence:D8}";
    }

    public void SetMapLevel(int mapLevel)
    {
        if (mapLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(mapLevel), "Map level must be positive.");
        }

        MapLevel = mapLevel;
    }

    public void Restore(ulong runSeed, int itemSequence, int mapLevel, ulong lootRandomState, ulong craftingRandomState, ulong eventRandomState)
    {
        if (itemSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(itemSequence), "Item sequence cannot be negative.");
        }

        SetMapLevel(mapLevel);
        RunSeed = runSeed;
        ItemSequence = itemSequence;
        LootRandom.RestoreState(lootRandomState);
        CraftingRandom.RestoreState(craftingRandomState);
        EventRandom.RestoreState(eventRandomState);
    }

    public LootGenerator CreateLootGenerator() => new(LootRandom, this);

    public LootGenerator CreateCraftingGenerator() => new(CraftingRandom, this);
}
