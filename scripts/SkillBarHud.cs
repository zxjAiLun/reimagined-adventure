using Arpg.Domain;
using Godot;

public partial class SkillBarHud : Label
{
    private PlayerSkillController _controller;
    private PlayerController _player;

    public override void _Ready()
    {
        _controller = GetNodeOrNull<PlayerSkillController>("../../Player/SkillBarController");
        _player = GetNodeOrNull<PlayerController>("../../Player");
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
        var primary = SkillName(SkillSlot.Primary, "Spread Shot");
        var secondary = SkillName(SkillSlot.Secondary, "Meteor");
        var utility = SkillName(SkillSlot.Utility, "Pulse");
        var movement = SkillName(SkillSlot.Movement, "Dash");
        Text = $"[左键/LMB] {primary}   [右键/RMB] {secondary}   [Q] {utility}   [Space] {movement}" + cooldowns + health;
    }

    private string SkillName(SkillSlot slot, string fallback)
    {
        return _controller?.Definition(slot).Name ?? fallback;
    }
}
