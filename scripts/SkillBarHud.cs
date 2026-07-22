using Godot;

public partial class SkillBarHud : Label
{
    private PlayerSkillController _controller;

    public override void _Ready()
    {
        _controller = GetNodeOrNull<PlayerSkillController>("../../Player/SkillBarController");
        RefreshText();
    }

    public override void _Process(double delta)
    {
        RefreshText();
    }

    private void RefreshText()
    {
        var cooldowns = _controller == null ? string.Empty : $"\n{_controller.CooldownStatusLine}";
        Text = "[左键/LMB] Spread Shot   [右键/RMB] Meteor   [Q] Pulse   [Space] Dash" + cooldowns;
    }
}
