using System;
using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

public partial class SkillLoadout3DRegressionSmoke : Node
{
    private PlayerController3D _player;
    private PlayerSkillController3D _skills;
    private double _elapsed;
    private int _stage;
    private int _baseProjectileCount;
    private int _baseProjectileDamage;
    private float _baseAreaRadius;
    private float _baseAreaCooldown;
    private float _pausedSecondaryCooldown;
    private string[] _unlockedSupportIds = Array.Empty<string>();
    private Dictionary<SkillSlot, string> _equippedSupports = new();
    private bool _complete;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _player = GetNode<PlayerController3D>("Player3D");
        _skills = _player.Skills;
    }

    public override void _Process(double delta)
    {
        if (_complete)
        {
            return;
        }

        _elapsed += delta;
        if (_elapsed > 12.0)
        {
            Fail("skill loadout smoke timed out at stage " + _stage);
            return;
        }

        try
        {
            switch (_stage)
            {
                case 0 when _elapsed >= 0.25:
                    BeginProjectileContract();
                    break;
                case 1 when _skills.CooldownRemaining(SkillSlot.Primary) <= 0.00001f:
                    VerifyVolleyContract();
                    break;
                case 2 when _skills.CooldownRemaining(SkillSlot.Primary) <= 0.00001f:
                    BeginAreaContract();
                    break;
                case 3 when _skills.CooldownRemaining(SkillSlot.Secondary) <= 0.00001f:
                    VerifyAmplifyContract();
                    break;
                case 4 when _elapsed >= 0.35:
                    VerifyPausedCooldown();
                    break;
                case 5 when _skills.CooldownRemaining(SkillSlot.Secondary) <= 0.00001f:
                    VerifyDetachedBaseContract();
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    private void BeginProjectileContract()
    {
        _skills.TryDetachSupport(SkillSlot.Primary);
        _skills.TryDetachSupport(SkillSlot.Secondary);
        if (!_skills.TryCastForTest(SkillSlot.Primary))
        {
            throw new InvalidOperationException("base Spread Shot did not cast");
        }

        _baseProjectileCount = _player.LastSpreadProjectileCount;
        _baseProjectileDamage = _player.LastSpreadProjectileDamage;
        if (_baseProjectileCount != 3)
        {
            throw new InvalidOperationException("base projectile count was not 3");
        }

        _stage = 1;
    }

    private void VerifyVolleyContract()
    {
        var attached = _skills.TryAttachSupport(SkillSlot.Primary, "volley");
        var cast = attached && _skills.TryCastForTest(SkillSlot.Primary);
        if (!attached || !cast)
        {
            throw new InvalidOperationException($"Volley did not attach and cast attached={attached} cast={cast} cooldown={_skills.CooldownRemaining(SkillSlot.Primary):0.000} supports={_skills.SupportCount(SkillSlot.Primary)}");
        }

        if (_player.LastSpreadProjectileCount <= _baseProjectileCount
            || _player.LastSpreadProjectileDamage > _baseProjectileDamage
            || _player.LastSpreadProjectileDamage < 1)
        {
            throw new InvalidOperationException(
                $"Volley did not change real projectile count and damage count={_player.LastSpreadProjectileCount}/{_baseProjectileCount} damage={_player.LastSpreadProjectileDamage}/{_baseProjectileDamage}");
        }

        _stage = 2;
    }

    private void BeginAreaContract()
    {
        _skills.TryDetachSupport(SkillSlot.Secondary);
        if (!_skills.TryCastAreaAtForTest(SkillSlot.Secondary, new Vector3(1.0f, 0.0f, 1.0f)))
        {
            throw new InvalidOperationException("base Meteor did not cast");
        }

        _baseAreaRadius = _player.LastAreaRadius;
        _baseAreaCooldown = _skills.CooldownRemaining(SkillSlot.Secondary);
        _stage = 3;
    }

    private void VerifyAmplifyContract()
    {
        if (!_skills.TryAttachSupport(SkillSlot.Secondary, "amplify")
            || !_skills.TryCastAreaAtForTest(SkillSlot.Secondary, new Vector3(1.0f, 0.0f, 1.0f)))
        {
            throw new InvalidOperationException("Amplify did not attach and cast");
        }

        if (_player.LastAreaRadius <= _baseAreaRadius
            || _skills.CooldownRemaining(SkillSlot.Secondary) <= _baseAreaCooldown)
        {
            throw new InvalidOperationException("Amplify did not change real area radius and cooldown");
        }

        if (_skills.TryAttachSupport(SkillSlot.Primary, "amplify")
            || _skills.TryAttachSupport(SkillSlot.Secondary, "volley"))
        {
            throw new InvalidOperationException("incompatible support was accepted at runtime");
        }

        _unlockedSupportIds = _skills.UnlockedSupportIds.ToArray();
        _equippedSupports = _skills.SupportIdBySkillSlot.ToDictionary(pair => pair.Key, pair => pair.Value);
        _pausedSecondaryCooldown = _skills.CooldownRemaining(SkillSlot.Secondary);
        GetTree().Paused = true;
        _elapsed = 0.0;
        _stage = 4;
    }

    private void VerifyPausedCooldown()
    {
        if (!Mathf.IsEqualApprox(_skills.CooldownRemaining(SkillSlot.Secondary), _pausedSecondaryCooldown))
        {
            GetTree().Paused = false;
            throw new InvalidOperationException(
                $"paused cooldown advanced from {_pausedSecondaryCooldown:0.000} to {_skills.CooldownRemaining(SkillSlot.Secondary):0.000}");
        }

        GetTree().Paused = false;
        _elapsed = 0.0;
        _stage = 5;
    }

    private void VerifyDetachedBaseContract()
    {
        if (!_skills.TryDetachSupport(SkillSlot.Secondary)
            || !_skills.TryCastAreaAtForTest(SkillSlot.Secondary, new Vector3(1.0f, 0.0f, 1.0f)))
        {
            throw new InvalidOperationException("detached Meteor did not cast");
        }

        if (!Mathf.IsEqualApprox(_player.LastAreaRadius, _baseAreaRadius)
            || _skills.CooldownRemaining(SkillSlot.Secondary) <= 0.0f)
        {
            throw new InvalidOperationException("detaching Amplify did not restore the base area contract");
        }

        _skills.TryDetachSupport(SkillSlot.Primary);
        _skills.TryDetachSupport(SkillSlot.Secondary);
        if (!_skills.TryRestoreLoadout(_unlockedSupportIds, _equippedSupports)
            || _skills.SupportCount(SkillSlot.Primary) != 1
            || _skills.SupportCount(SkillSlot.Secondary) != 1)
        {
            throw new InvalidOperationException("support loadout did not restore exactly once");
        }

        _complete = true;
        GD.Print("SKILL_LOADOUT_3D_SPIKE_PASS volley=true amplify=true runtime=true save_shape=true no_duplicate=true");
        GetTree().Quit();
    }

    private void Fail(string reason)
    {
        _complete = true;
        GD.PushError("SKILL_LOADOUT_3D_SPIKE_FAIL " + reason);
        GetTree().Quit(1);
    }
}
