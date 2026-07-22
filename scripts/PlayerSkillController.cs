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

        if (Input.IsActionJustPressed("skill_spread_shot"))
        {
            TryCast(SkillSlot.Primary);
        }

        if (Input.IsActionJustPressed("skill_meteor"))
        {
            TryCast(SkillSlot.Secondary);
        }

        if (Input.IsActionJustPressed("skill_pulse"))
        {
            TryCast(SkillSlot.Utility);
        }

        if (Input.IsActionJustPressed("skill_dash"))
        {
            TryCast(SkillSlot.Movement);
        }
    }

    public SkillDefinition Definition(SkillSlot slot) => _skillBar[slot];

    private void TryCast(SkillSlot slot)
    {
        if (_player == null || _cooldowns[slot] > 0.0f)
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
