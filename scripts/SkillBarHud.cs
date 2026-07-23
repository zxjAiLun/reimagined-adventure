using Arpg.Domain;
using Godot;

public partial class SkillBarHud : Label
{
    private PlayerSkillController _controller;
    private PlayerController _player;
    private InventoryController _inventory;

    public override void _Ready()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
        _controller = _player?.GetNodeOrNull<PlayerSkillController>("SkillBarController");
        _inventory = _player?.Inventory;
        RefreshText();
    }

    public override void _Process(double delta)
    {
        RefreshText();
    }

    private void RefreshText()
    {
        var cooldowns = _controller == null ? string.Empty : $"\n{_controller.CooldownStatusLine}";
        var health = _player == null ? string.Empty : $"\nHP {_player.CurrentHealth}/{_player.MaxHealth}";
        var inventory = _inventory == null ? string.Empty : $"\n[F] Pick up   [E] Equip newest   {_inventory.InventorySummary()}";
        var primary = SkillName(SkillSlot.Primary, "Spread Shot");
        var secondary = SkillName(SkillSlot.Secondary, "Meteor");
        var utility = SkillName(SkillSlot.Utility, "Pulse");
        var movement = SkillName(SkillSlot.Movement, "Dash");
        Text = $"[左键/LMB] {primary}   [右键/RMB] {secondary}   [Q] {utility}   [Space] {movement}" + cooldowns + health + inventory;
    }

    private string SkillName(SkillSlot slot, string fallback)
    {
        return _controller?.Definition(slot).Name ?? fallback;
    }
}
