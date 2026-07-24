using System;
using System.Linq;
using Arpg.Domain;
using Godot;

public partial class CombatHud3DRegressionSmoke : Node
{
    private double _elapsed;
    private int _stage;
    private bool _complete;
    private int _baselineSpreadDamage;

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_complete)
        {
            return;
        }

        var run = GetTree().GetFirstNodeInGroup("run_sessions") as RunSessionNode;
        var arena = GetTree().GetNodesInGroup("arena_3d")
            .OfType<TestArena3D>()
            .LastOrDefault();
        var hud = arena?.GetNodeOrNull<CombatHudController3D>("HUD");
        var player = arena?.GetNodeOrNull<PlayerController3D>("Player3D");
        var skills = player?.GetNodeOrNull<PlayerSkillController3D>("PlayerSkillController3D");
        var flow = arena?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        var rewards = arena?.GetNodeOrNull<MapRewardNode3D>("MapRewards3D");
        var boss = arena?.GetNodeOrNull<BrimstoneColossusController3D>("BrimstoneColossus3D");
        var flowLabel = hud?.GetNodeOrNull<Label>("PlayerPanel/FlowState");
        var primaryLabel = hud?.GetNodeOrNull<Label>("SkillPanel/Primary");
        var secondaryLabel = hud?.GetNodeOrNull<Label>("SkillPanel/Secondary");
        var utilityLabel = hud?.GetNodeOrNull<Label>("SkillPanel/Utility");
        var movementLabel = hud?.GetNodeOrNull<Label>("SkillPanel/Movement");
        if (run == null || arena == null || hud == null || player == null || skills == null
            || flow == null || rewards == null || boss == null || flowLabel == null
            || primaryLabel == null || secondaryLabel == null || utilityLabel == null
            || movementLabel == null)
        {
            if (_elapsed > 10.0)
            {
                Fail("HUD runtime nodes did not become ready");
            }

            return;
        }

        switch (_stage)
        {
            case 0:
                if (hud.MapLevel != run.CurrentMapLevel
                    || hud.MapLevel != 1
                    || !hud.BossPanelVisible
                    || hud.BossCurrentHealth != boss.CurrentHealth
                    || flowLabel.Text != "State: Playing"
                    || !primaryLabel.Text.Contains("Spread Shot")
                    || !secondaryLabel.Text.Contains("Meteor")
                    || !utilityLabel.Text.Contains("Pulse")
                    || !movementLabel.Text.Contains("Dash"))
                {
                    Fail("initial HUD data is incorrect");
                    return;
                }

                player.ApplyDamage(new DamageRequest(
                    5,
                    DamageType.Physical,
                    "combat_hud_health_smoke",
                    CombatFaction.Enemy));
                _stage = 1;
                return;

            case 1:
                if (hud.PlayerCurrentHealth != player.CurrentHealth
                    || hud.PlayerMaxHealth != player.MaxHealth
                    || hud.PlayerHealthBarValue != player.CurrentHealth
                    || hud.PlayerHealthBarMax != player.MaxHealth)
                {
                    Fail($"player HP HUD did not update hp={hud.PlayerHealthText} bar={hud.PlayerHealthBarValue}/{hud.PlayerHealthBarMax}");
                    return;
                }

                _baselineSpreadDamage = hud.SpreadShotDamage;
                if (!player.RestoreInventory(Array.Empty<Item>(), CreateMaxHpWeapon()))
                {
                    Fail("could not equip MaxHP test weapon");
                    return;
                }

                _stage = 2;
                return;

            case 2:
                if (hud.PlayerMaxHealth != 125
                    || hud.PlayerMaxHealth != player.MaxHealth
                    || hud.EquippedWeaponText == "none"
                    || hud.SpreadShotDamage == _baselineSpreadDamage)
                {
                    Fail($"equipment HUD did not update max={hud.PlayerMaxHealth} weapon={hud.EquippedWeaponText} damage={hud.SpreadShotDamage}");
                    return;
                }

                if (!skills.TryCastForTest(SkillSlot.Primary))
                {
                    Fail("Spread Shot could not be cast for cooldown HUD test");
                    return;
                }

                _stage = 3;
                return;

            case 3:
                if (hud.CooldownRemaining(SkillSlot.Primary) <= 0.0f
                    || hud.CooldownRemaining(SkillSlot.Primary) != skills.CooldownRemaining(SkillSlot.Primary)
                    || primaryLabel.Text.Contains("CD 0.00")
                    || !primaryLabel.Text.Contains("Spread Shot")
                    || !secondaryLabel.Text.Contains("Meteor")
                    || !utilityLabel.Text.Contains("Pulse")
                    || !movementLabel.Text.Contains("Dash")
                    || !hud.BossPanelVisible
                    || hud.BossHealthBarValue != boss.CurrentHealth
                    || hud.BossHealthBarMax != boss.MaxHealth)
                {
                    Fail($"skill or boss HUD did not update cooldown={hud.CooldownRemaining(SkillSlot.Primary)} boss={hud.BossHealthText}");
                    return;
                }

                boss.ApplyDamage(new DamageRequest(
                    9999,
                    DamageType.Physical,
                    "combat_hud_boss_smoke",
                    CombatFaction.Player));
                _stage = 4;
                return;

            case 4:
                if (boss.IsAlive || hud.BossPanelVisible || flowLabel.Text != "State: MapComplete")
                {
                    if (_elapsed > 15.0)
                    {
                        Fail("boss HUD did not hide after boss death");
                    }

                    return;
                }

                if (flow.State != GameFlowState.MapComplete || !rewards.TryChooseReward(0))
                {
                    Fail($"could not complete reward flow state={flow.State}");
                    return;
                }

                if (!run.LoadNextMap())
                {
                    Fail("could not load next map for Map Level HUD test");
                    return;
                }

                _stage = 5;
                return;

            case 5:
                if (run.CurrentMapLevel < 2)
                {
                    return;
                }

                var nextArena = GetTree().GetNodesInGroup("arena_3d")
                    .OfType<TestArena3D>()
                    .LastOrDefault();
                var nextHud = nextArena?.GetNodeOrNull<CombatHudController3D>("HUD");
                if (nextHud == null || nextHud.MapLevel != 2)
                {
                    if (_elapsed > 18.0)
                    {
                        Fail($"next map HUD did not update map level={nextHud?.MapLevel}");
                    }

                    return;
                }

                _complete = true;
                GD.Print("COMBAT_HUD_3D_SPIKE_PASS player_hp=true max_hp_equipment=true skills=true cooldown=true boss_visible=true boss_hidden=true map_level=true signal_driven=true");
                GetTree().Quit();
                return;
        }

        if (_elapsed > 20.0)
        {
            Fail($"HUD smoke timed out at stage {_stage}");
        }
    }

    private void Fail(string reason)
    {
        _complete = true;
        GD.PushError($"COMBAT_HUD_3D_SPIKE_FAIL {reason}");
        GetTree().Quit(1);
    }

    private static Item CreateMaxHpWeapon()
    {
        var baseDefinition = ItemBaseLibrary.Find("rustbound_blade");
        if (baseDefinition == null)
        {
            throw new InvalidOperationException("rustbound_blade test base is missing");
        }

        var vitalityAffix = new Affix
        {
            Id = "combat_hud_vitality",
            Name = "Vitality",
            Tier = 1,
            IsPrefix = true,
            Stats = new Stats
            {
                MaxHp = 25,
                DamageMultiplier = 5.0,
            },
        };
        var item = new Item
        {
            Id = "combat_hud_vitality_weapon",
            Name = "Combat HUD Vitality Blade",
            BaseId = baseDefinition.Id,
            Slot = baseDefinition.Slot,
            RequiredLevel = baseDefinition.RequiredLevel,
            Stats = Stats.Combine(baseDefinition.ImplicitStats, vitalityAffix.Stats),
            Affixes = [vitalityAffix],
        };
        item.Validate();
        return item;
    }
}
