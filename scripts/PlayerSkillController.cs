using System;
using System.Collections.Generic;
using Arpg.Domain;
using Godot;

public partial class PlayerSkillController : Node
{
    [Export] public PackedScene AreaEffectScene { get; set; }

    private readonly SkillBar _skillBar = SkillLibrary.DefaultBar();
    private readonly Dictionary<SkillSlot, float> _cooldowns = new();
    private PlayerController _player;

    public string CooldownStatusLine
    {
        get
        {
            return $"CD  LMB {_cooldowns[SkillSlot.Primary]:0.00}  "
                + $"RMB {_cooldowns[SkillSlot.Secondary]:0.00}  "
                + $"Q {_cooldowns[SkillSlot.Utility]:0.00}  "
                + $"Space {_cooldowns[SkillSlot.Movement]:0.00}";
        }
    }

    public override void _Ready()
    {
        SetProcessInput(true);
        _player = GetParent<PlayerController>();
        foreach (var slot in Enum.GetValues<SkillSlot>())
        {
            _cooldowns[slot] = 0.0f;
        }
    }

    public override void _Process(double delta)
    {
        var frameDelta = (float)delta;
        foreach (var slot in Enum.GetValues<SkillSlot>())
        {
            _cooldowns[slot] = Mathf.Max(0.0f, _cooldowns[slot] - frameDelta);
        }

        // Spread Shot is the primary attack, so holding LMB repeats it as
        // soon as its Domain cooldown allows the next cast.
        if (Input.IsMouseButtonPressed(MouseButton.Left))
        {
            TryCast(SkillSlot.Primary);
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            switch (mouseButton.ButtonIndex)
            {
                case MouseButton.Left:
                    TryCast(SkillSlot.Primary);
                    break;
                case MouseButton.Right:
                    TryCast(SkillSlot.Secondary);
                    break;
            }

            return;
        }

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            var isQ = keyEvent.Keycode == Key.Q
                || keyEvent.PhysicalKeycode == Key.Q
                || keyEvent.Unicode == 'q'
                || keyEvent.Unicode == 'Q';
            var isSpace = keyEvent.Keycode == Key.Space
                || keyEvent.PhysicalKeycode == Key.Space
                || keyEvent.Unicode == ' ';

            if (isQ)
            {
                TryCast(SkillSlot.Utility);
            }
            else if (isSpace)
            {
                TryCast(SkillSlot.Movement);
            }
        }
    }

    public SkillDefinition Definition(SkillSlot slot) => _skillBar[slot];

    private void TryCast(SkillSlot slot)
    {
        if (_player == null || !_player.IsAlive || _cooldowns[slot] > 0.0f)
        {
            return;
        }

        var definition = _skillBar[slot];
        var castSucceeded = false;
        switch (definition.CastType)
        {
            case SkillCastType.Projectile:
                castSucceeded = _player.CastSpreadShot(definition);
                break;
            case SkillCastType.MouseTargetedArea:
                castSucceeded = _player.CastAreaSkill(definition, _player.GetGlobalMousePosition(), AreaEffectScene);
                break;
            case SkillCastType.SelfCenteredArea:
                castSucceeded = _player.CastAreaSkill(definition, _player.GlobalPosition, AreaEffectScene);
                break;
            case SkillCastType.Dash:
                _player.PerformDash(definition.DashDistance);
                castSucceeded = true;
                break;
            default:
                return;
        }

        if (castSucceeded)
        {
            _cooldowns[slot] = (float)definition.CooldownSeconds;
        }
    }
}
