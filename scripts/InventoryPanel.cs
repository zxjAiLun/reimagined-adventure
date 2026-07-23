using Godot;

/// <summary>
/// First Control/Container inventory view. It is intentionally read-only for
/// now; F/E remain the deterministic interaction contract for the slice.
/// </summary>
public partial class InventoryPanel : PanelContainer
{
    private Label _contents;
    private InventoryController _inventory;

    public override void _Ready()
    {
        _contents = GetNodeOrNull<Label>("Margin/VBox/InventoryText");
        Refresh();
    }

    public override void _Process(double delta)
    {
        Refresh();
    }

    private void Refresh()
    {
        _inventory ??= (GetTree().GetFirstNodeInGroup("player") as PlayerController)?.Inventory;
        if (_contents == null || _inventory == null)
        {
            return;
        }

        var text = _inventory.InventorySummary();
        if (_inventory.ItemCount == 0)
        {
            text += "\n\nNo items in bag";
        }
        else
        {
            text += "\n\nItems:";
            for (var index = 0; index < _inventory.ItemCount; index++)
            {
                var item = _inventory.Items[index];
                text += $"\n{index + 1}. {item.Name} ({item.RarityName})";
            }
        }

        _contents.Text = text;
    }
}
