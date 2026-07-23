namespace Arpg.Domain;

/// <summary>
/// Persistent item storage kept separate from the run inventory. The first
/// content slice only needs deterministic tab capacity and item identity
/// rules; serialization remains part of the later save milestone.
/// </summary>
public sealed class StashTab
{
    private readonly List<Item> _items = new();

    public required string Id { get; init; }
    public required string Name { get; init; }
    public int Capacity { get; init; } = 24;
    public IReadOnlyList<Item> Items => _items;

    public bool TryDeposit(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Validate();
        if (_items.Count >= Capacity || _items.Any(existing => existing.Id == item.Id))
        {
            return false;
        }

        _items.Add(item);
        return true;
    }

    public Item? Withdraw(string itemId)
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

    public bool Contains(string itemId) =>
        !string.IsNullOrWhiteSpace(itemId) && _items.Any(item => item.Id == itemId);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Stash tab id and name are required.");
        }

        if (Capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Capacity), "Stash tab capacity must be positive.");
        }

        if (_items.Count > Capacity)
        {
            throw new ArgumentException("Stash tab contains more items than its capacity.", nameof(Items));
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in _items)
        {
            ArgumentNullException.ThrowIfNull(item);
            item.Validate();
            if (!ids.Add(item.Id))
            {
                throw new ArgumentException($"Stash tab contains duplicate item id '{item.Id}'.", nameof(Items));
            }
        }
    }
}

public sealed class Stash
{
    private readonly Dictionary<string, StashTab> _tabs = new(StringComparer.Ordinal);

    public IReadOnlyCollection<StashTab> Tabs => _tabs.Values;

    public Stash(IEnumerable<StashTab> tabs)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        foreach (var tab in tabs)
        {
            AddTab(tab);
        }

        if (_tabs.Count == 0)
        {
            throw new ArgumentException("Stash must contain at least one tab.", nameof(tabs));
        }
    }

    public StashTab? FindTab(string? tabId) =>
        string.IsNullOrWhiteSpace(tabId) ? null : _tabs.GetValueOrDefault(tabId);

    public bool TryDeposit(string tabId, Item item)
    {
        var tab = FindTab(tabId);
        if (tab == null || item == null || ContainsItemId(item.Id))
        {
            return false;
        }

        return tab.TryDeposit(item);
    }

    public Item? Withdraw(string tabId, string itemId)
    {
        return FindTab(tabId)?.Withdraw(itemId);
    }

    public bool ContainsItemId(string? itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId)
            && _tabs.Values.Any(tab => tab.Contains(itemId));
    }

    public void AddTab(StashTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        tab.Validate();
        if (!_tabs.TryAdd(tab.Id, tab))
        {
            throw new ArgumentException($"Stash tab id '{tab.Id}' is duplicated.", nameof(tab));
        }

        try
        {
            ValidateUniqueItemIds();
        }
        catch
        {
            _tabs.Remove(tab.Id);
            throw;
        }
    }

    public void Validate()
    {
        if (_tabs.Count == 0)
        {
            throw new ArgumentException("Stash must contain at least one tab.");
        }

        foreach (var tab in _tabs.Values)
        {
            tab.Validate();
        }

        ValidateUniqueItemIds();
    }

    public static Stash CreateDefault() => new(
    [
        new StashTab { Id = "default", Name = "Default", Capacity = 24 },
    ]);

    private void ValidateUniqueItemIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in _tabs.Values.SelectMany(tab => tab.Items))
        {
            if (!ids.Add(item.Id))
            {
                throw new ArgumentException($"Stash contains duplicate item id '{item.Id}'.");
            }
        }
    }
}
