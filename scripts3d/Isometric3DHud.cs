using Godot;
using Arpg.Domain;

public partial class Isometric3DHud : CanvasLayer
{
    private PlayerController3D _player;
    private FeralController3D _feral;
    private PlayerSkillController3D _skills;
    private Label _status;

    public override void _Ready()
    {
        _player = GetTree().GetFirstNodeInGroup("player_3d") as PlayerController3D;
        _feral = GetTree().GetFirstNodeInGroup("enemies_3d") as FeralController3D;
        _skills = _player?.GetNodeOrNull<PlayerSkillController3D>("PlayerSkillController3D");
        _status = GetNode<Label>("Status");
    }

    public override void _Process(double delta)
    {
        if (_player == null || !IsInstanceValid(_player))
        {
            _player = GetTree().GetFirstNodeInGroup("player_3d") as PlayerController3D;
        }

        if (_feral == null || !IsInstanceValid(_feral))
        {
            _feral = GetTree().GetFirstNodeInGroup("enemies_3d") as FeralController3D;
        }

        if (_skills == null && _player != null)
        {
            _skills = _player.GetNodeOrNull<PlayerSkillController3D>("PlayerSkillController3D");
        }

        if (_player == null || _status == null)
        {
            return;
        }

        var feralText = _feral == null
            ? "Feral: defeated"
            : $"Feral: {_feral.CurrentHealth}/{_feral.MaxHealth} ({_feral.State})";
        _status.Text = $"HP: {_player.CurrentHealth}/{_player.MaxHealth}\n"
            + $"Aim: ({_player.AimDirection.X:0.00}, {_player.AimDirection.Z:0.00})\n"
            + $"{feralText}\n"
            + $"{_player.InventorySummary()}\n"
            + $"{_skills?.CooldownStatusLine ?? "Skills loading..."}";
    }
}
