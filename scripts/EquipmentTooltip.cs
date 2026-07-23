using Godot;

/// <summary>
/// A small Panel-based equipped-item tooltip. Detailed affix presentation is
/// deferred, but the actual aggregated weapon stats are shown here.
/// </summary>
public partial class EquipmentTooltip : PanelContainer
{
    private Label _contents;
    private InventoryController _inventory;

    public override void _Ready()
    {
        _contents = GetNodeOrNull<Label>("Margin/TooltipText");
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

        var item = _inventory.EquippedWeapon;
        _contents.Text = item == null
            ? "EQUIPMENT\nWeapon slot empty"
            : $"EQUIPPED WEAPON\n{item.Name}\nDamage x{item.Stats.DamageMultiplier:0.00}\nProjectile x{item.Stats.ProjectileDamageMultiplier:0.00}\nSpread Shot: {_inventory.SpreadShotDamage}";
    }
}
