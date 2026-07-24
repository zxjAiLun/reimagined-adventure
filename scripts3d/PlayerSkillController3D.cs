using System;
using System.Collections.Generic;
using Arpg.Domain;
using Godot;

/// <summary>
/// Owns 3D skill input, slot definitions and cooldowns. The player facade
/// performs the spatial cast; Domain remains the source of skill arithmetic.
/// </summary>
public partial class PlayerSkillController3D : Node
{
    [Signal]
    public delegate void CooldownsChangedEventHandler();

    [Export] public PackedScene AreaEffectScene { get; set; }
    [Export] public SkillBarResource SkillBarResource { get; set; }

    private readonly Dictionary<SkillSlot, float> _cooldowns = new();
    private readonly Dictionary<SkillSlot, IReadOnlyList<SupportDefinition>> _supports = new();
    private PlayerController3D _player;
    private PlayerAim3D _aim;
    private SkillBar _skillBar;

    public string CooldownStatusLine =>
        $"CD LMB {_cooldowns[SkillSlot.Primary]:0.00} "
        + $"RMB {_cooldowns[SkillSlot.Secondary]:0.00} "
        + $"Q {_cooldowns[SkillSlot.Utility]:0.00} "
        + $"Space {_cooldowns[SkillSlot.Movement]:0.00}";

    public override void _Ready()
    {
        SetProcessUnhandledInput(true);
        _player = GetParent<PlayerController3D>();
        _aim = _player.GetNode<PlayerAim3D>("PlayerAim3D");
        _skillBar = SkillBarResource?.ToDomain() ?? SkillLibrary.DefaultBar();
        foreach (var slot in Enum.GetValues<SkillSlot>())
        {
            _cooldowns[slot] = 0.0f;
            _supports[slot] = SkillBarResource?.SupportsFor(slot) ?? Array.Empty<SupportDefinition>();
        }
    }

    public override void _Process(double delta)
    {
        var frameDelta = (float)delta;
        var changed = false;
        foreach (var slot in Enum.GetValues<SkillSlot>())
        {
            var previous = _cooldowns[slot];
            _cooldowns[slot] = Mathf.Max(0.0f, previous - frameDelta);
            changed |= !Mathf.IsEqualApprox(previous, _cooldowns[slot]);
        }

        if (changed)
        {
            EmitSignal(SignalName.CooldownsChanged);
        }

        if (Input.IsActionPressed("skill_spread_shot"))
        {
            TryCast(SkillSlot.Primary);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("skill_meteor", true))
        {
            TryCast(SkillSlot.Secondary);
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("skill_pulse", true))
        {
            TryCast(SkillSlot.Utility);
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("skill_dash", true))
        {
            TryCast(SkillSlot.Movement);
            GetViewport().SetInputAsHandled();
        }
    }

    public SkillDefinition Definition(SkillSlot slot) => _skillBar[slot];

    public float CooldownRemaining(SkillSlot slot) => _cooldowns[slot];

    public int SupportCount(SkillSlot slot) => _supports[slot].Count;

    public IReadOnlyList<SupportDefinition> Supports(SkillSlot slot) => _supports[slot];

    public bool TryCastForTest(SkillSlot slot) => TryCast(slot);

    private bool TryCast(SkillSlot slot)
    {
        if (_player == null || !_player.IsAlive || _cooldowns[slot] > 0.0f)
        {
            return false;
        }

        var definition = _skillBar[slot];
        var supports = _supports[slot];
        var castSucceeded = definition.CastType switch
        {
            SkillCastType.Projectile => _player.CastSpreadShot(definition, supports),
            SkillCastType.MouseTargetedArea => TryCastMouseArea(definition, supports),
            SkillCastType.SelfCenteredArea => _player.CastAreaSkill(
                definition,
                _player.GlobalPosition,
                AreaEffectScene,
                supports),
            SkillCastType.Dash => _player.PerformDash(
                SpatialScale3D.Distance(definition.DashDistance)),
            _ => false,
        };

        if (!castSucceeded)
        {
            return false;
        }

        _cooldowns[slot] = (float)SkillSupportMath.Cooldown(
            definition,
            _player.EffectiveStats,
            supports);
        EmitSignal(SignalName.CooldownsChanged);
        return true;
    }

    private bool TryCastMouseArea(
        SkillDefinition definition,
        IReadOnlyList<SupportDefinition> supports)
    {
        if (_aim == null || !_aim.TryGetGroundPoint(out var point))
        {
            return false;
        }

        return _player.CastAreaSkill(definition, point, AreaEffectScene, supports);
    }
}
