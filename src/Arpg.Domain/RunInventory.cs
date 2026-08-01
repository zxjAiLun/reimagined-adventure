namespace Arpg.Domain;

public sealed record EquipTransactionResult(
    bool Succeeded,
    Item? EquippedItem,
    Item? ReplacedItem,
    string Error)
{
    public static EquipTransactionResult Failed(string error) =>
        new(false, null, null, error);
}

public sealed record UnequipTransactionResult(
    bool Succeeded,
    Item? UnequippedItem,
    string Error)
{
    public static UnequipTransactionResult Failed(string error) =>
        new(false, null, error);
}

/// <summary>
/// Run-owned inventory. It is the only owner of loose items; Equipment owns
/// equipped items. Transactions validate before mutating either side.
/// </summary>
public sealed class RunInventory
{
    private readonly List<Item> _items = new();

    public RunInventory(int capacity = 16)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Inventory capacity must be positive.");
        }

        Capacity = capacity;
    }

    public int Capacity { get; }
    public IReadOnlyList<Item> Items => _items;
    public int Count => _items.Count;

    public bool Contains(string? itemId) =>
        !string.IsNullOrWhiteSpace(itemId)
        && _items.Any(item => item.Id == itemId);

    public bool CanAccept(Item item, Equipment? equipment = null)
    {
        if (item == null || _items.Count >= Capacity || Contains(item.Id))
        {
            return false;
        }

        return equipment == null || !equipment.ContainsItemId(item.Id);
    }

    public bool TryAdd(Item item, Equipment? equipment = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Validate();
        if (!CanAccept(item, equipment))
        {
            return false;
        }

        _items.Add(item);
        return true;
    }

    public Item? Remove(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        var index = _items.FindIndex(item => item.Id == itemId);
        if (index < 0)
        {
            return null;
        }

        var item = _items[index];
        _items.RemoveAt(index);
        return item;
    }

    public EquipTransactionResult TryEquip(string itemId, Equipment equipment, int playerLevel = 1)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        var index = _items.FindIndex(item => item.Id == itemId);
        if (index < 0)
        {
            return EquipTransactionResult.Failed("item is not in inventory");
        }

        var item = _items[index];
        if (!equipment.CanEquip(item, playerLevel))
        {
            return EquipTransactionResult.Failed("item cannot be equipped");
        }

        // CanEquip is the only fallible rule. Equip itself now cannot fail, so
        // the swap below is a single deterministic state transition.
        var replaced = equipment.Equip(item, playerLevel);
        if (replaced == null)
        {
            _items.RemoveAt(index);
        }
        else
        {
            _items[index] = replaced;
        }

        return new EquipTransactionResult(true, item, replaced, string.Empty);
    }

    public UnequipTransactionResult TryUnequip(EquipmentSlot slot, Equipment equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        var equipped = equipment.ItemInSlot(slot);
        if (equipped == null)
        {
            return UnequipTransactionResult.Failed("slot is empty");
        }

        if (!CanAccept(equipped))
        {
            return UnequipTransactionResult.Failed("inventory is full or item identity is already present");
        }

        equipment.Unequip(slot);
        _items.Add(equipped);
        return new UnequipTransactionResult(true, equipped, string.Empty);
    }

    public void Clear() => _items.Clear();

    public void Restore(IEnumerable<Item> items, Equipment? equipment = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        var restored = items.ToArray();
        if (restored.Length > Capacity)
        {
            throw new ArgumentException("Restored items exceed inventory capacity.", nameof(items));
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in restored)
        {
            ArgumentNullException.ThrowIfNull(item);
            item.Validate();
            if (!ids.Add(item.Id) || equipment?.ContainsItemId(item.Id) == true)
            {
                throw new ArgumentException("Restored item identities must be unique.", nameof(items));
            }
        }

        _items.Clear();
        _items.AddRange(restored);
    }
}
