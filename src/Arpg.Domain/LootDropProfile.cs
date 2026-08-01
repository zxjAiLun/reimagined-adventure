namespace Arpg.Domain;

public sealed record LootDropProfile(
    int BaseDropChancePercent,
    int MaximumItemDrops,
    int ForgeFragmentChancePercent,
    int ForgeFragmentAmount,
    bool GuaranteedItem)
{
    public void Validate()
    {
        if (BaseDropChancePercent is < 0 or > 100
            || ForgeFragmentChancePercent is < 0 or > 100
            || MaximumItemDrops < 0
            || ForgeFragmentAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BaseDropChancePercent), "Loot profile values are out of range.");
        }

        if (GuaranteedItem && MaximumItemDrops < 1)
        {
            throw new ArgumentException("A guaranteed item profile needs at least one item roll.", nameof(MaximumItemDrops));
        }
    }
}

public sealed record LootDropResult(
    IReadOnlyList<Item> Items,
    int ForgeFragments)
{
    public int ItemCount => Items.Count;
}

public static class LootDropProfiles
{
    public static LootDropProfile Feral { get; } = new(
        BaseDropChancePercent: 25,
        MaximumItemDrops: 1,
        ForgeFragmentChancePercent: 10,
        ForgeFragmentAmount: 1,
        GuaranteedItem: false);

    public static LootDropProfile Spitter { get; } = new(
        BaseDropChancePercent: 35,
        MaximumItemDrops: 1,
        ForgeFragmentChancePercent: 15,
        ForgeFragmentAmount: 1,
        GuaranteedItem: false);

    public static LootDropProfile Boss { get; } = new(
        BaseDropChancePercent: 100,
        MaximumItemDrops: 2,
        ForgeFragmentChancePercent: 100,
        ForgeFragmentAmount: 3,
        GuaranteedItem: true);
}
