namespace Arpg.Domain;

public sealed class Equipment
{
    private readonly Dictionary<EquipmentSlot, Item> _equipped = new();

    public IReadOnlyDictionary<EquipmentSlot, Item> Items => _equipped;

    public Item? ItemInSlot(EquipmentSlot slot)
    {
        return _equipped.GetValueOrDefault(slot);
    }

    public bool CanEquip(Item item, int playerLevel = 1)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (playerLevel < 1)
        {
            return false;
        }

        try
        {
            item.Validate();
        }
        catch (ArgumentException)
        {
            return false;
        }

        var baseDefinition = ItemBaseLibrary.Find(item.BaseId);
        return baseDefinition != null
            && baseDefinition.Slot == item.Slot
            && playerLevel >= Math.Max(item.RequiredLevel, baseDefinition.RequiredLevel);
    }

    /// <summary>
    /// Equips an item and returns the item previously occupying the same slot.
    /// The caller owns the returned item and can put it back into inventory.
    /// </summary>
    public Item? Equip(Item item, int playerLevel = 1)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!CanEquip(item, playerLevel))
        {
            throw new InvalidOperationException($"Cannot equip item '{item.Name}'.");
        }

        _equipped.TryGetValue(item.Slot, out var replaced);
        _equipped[item.Slot] = item;
        return replaced;
    }

    public Stats CombinedStats()
    {
        var result = Stats.Neutral;
        foreach (var item in _equipped.Values)
        {
            result = Stats.Combine(result, item.Stats);
        }

        return result;
    }

    public void Reset() => _equipped.Clear();
}
