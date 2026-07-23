using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// Godot ownership boundary for persistent stash tabs. The domain object
/// remains usable without this node or a running editor.
/// </summary>
public partial class StashNode : Node
{
    [Signal]
    public delegate void StashChangedEventHandler();

    [Export] public StashResource DefinitionResource { get; set; }

    private Stash _stash;

    public Stash DomainStash => _stash ?? throw new InvalidOperationException("StashNode is not ready.");

    public override void _Ready()
    {
        _stash = DefinitionResource?.ToDomain() ?? Stash.CreateDefault();
        AddToGroup("stash");
    }

    public bool TryDeposit(string tabId, Item item)
    {
        var deposited = DomainStash.TryDeposit(tabId, item);
        if (deposited)
        {
            EmitSignal(SignalName.StashChanged);
        }

        return deposited;
    }

    public Item Withdraw(string tabId, string itemId)
    {
        var item = DomainStash.Withdraw(tabId, itemId);
        if (item != null)
        {
            EmitSignal(SignalName.StashChanged);
        }

        return item;
    }

    public bool TryStoreInventoryItem(string tabId, PlayerController player, int inventoryIndex)
    {
        if (player?.Inventory == null
            || inventoryIndex < 0
            || inventoryIndex >= player.Inventory.ItemCount)
        {
            return false;
        }

        var item = player.Inventory.Items[inventoryIndex];
        if (!DomainStash.TryDeposit(tabId, item))
        {
            return false;
        }

        if (!player.Inventory.TryRemoveItem(inventoryIndex, out _))
        {
            DomainStash.Withdraw(tabId, item.Id);
            return false;
        }

        EmitSignal(SignalName.StashChanged);
        return true;
    }

    public bool TryWithdrawToInventory(string tabId, string itemId, PlayerController player)
    {
        if (player?.Inventory == null)
        {
            return false;
        }

        var item = DomainStash.Withdraw(tabId, itemId);
        if (item == null)
        {
            return false;
        }

        if (!player.Inventory.TryAddItem(item))
        {
            DomainStash.TryDeposit(tabId, item);
            return false;
        }

        EmitSignal(SignalName.StashChanged);
        return true;
    }
}
