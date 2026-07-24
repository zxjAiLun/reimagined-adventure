using System;
using Arpg.Domain;
using Godot;

public partial class SaveRecovery3DRegressionSmoke : Node
{
    private double _elapsed;
    private int _stage;
    private bool _complete;

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_complete)
        {
            return;
        }

        var arena = GetTree().GetFirstNodeInGroup("arena_3d") as TestArena3D;
        var player = arena?.GetNodeOrNull<PlayerController3D>("Player3D");
        var flow = arena?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        var save = arena?.GetNodeOrNull<SaveBoundaryNode3D>("SaveBoundary3D");
        if (player == null || flow == null || save == null)
        {
            if (_elapsed > 6.0)
            {
                Fail("3D recovery nodes did not become ready");
            }

            return;
        }

        switch (_stage)
        {
            case 0:
                player.SetRewardStats(new Stats
                {
                    DamageMultiplier = 1.2,
                    MaxHp = 25,
                });
                player.ApplyRestoredHealth(115);
                if (!save.TrySaveCurrentRun(out var saveError))
                {
                    Fail($"could not create Playing save: {saveError}");
                    return;
                }

                player.SetRewardStats(Stats.Neutral);
                _stage = 1;
                return;

            case 1:
                if (!save.TryLoadAndApplyLastRun(out _, out var rewardLoadError))
                {
                    Fail($"RewardStats.MaxHp save could not load: {rewardLoadError}");
                    return;
                }

                var rewardRestorePass = player.MaxHealth == 125
                    && player.CurrentHealth == 115
                    && player.RewardStats.MaxHp == 25
                    && player.RewardStats.DamageMultiplier > 1.0;
                if (!rewardRestorePass)
                {
                    Fail($"RewardStats.MaxHp restore failed max={player.MaxHealth} hp={player.CurrentHealth}");
                    return;
                }

                player.SetRewardStats(Stats.Neutral);
                save.InjectFailureAfterRewardForTest = true;
                _stage = 2;
                return;

            case 2:
                var rejected = !save.TryLoadAndApplyLastRun(out _, out _);
                var rollbackPass = rejected
                    && player.RewardStats.EquivalentTo(Stats.Neutral)
                    && player.MaxHealth == 100
                    && player.CurrentHealth == 100
                    && flow.State == GameFlowState.Playing;
                save.InjectFailureAfterRewardForTest = false;
                if (!rollbackPass)
                {
                    Fail("failed 3D restore did not roll back atomically");
                    return;
                }

                var maxHpWeapon = CreateMaxHpWeapon();
                if (!player.RestoreInventory(Array.Empty<Item>(), maxHpWeapon)
                    || player.MaxHealth != 125)
                {
                    Fail("could not prepare equipment MaxHp save");
                    return;
                }

                player.ApplyRestoredHealth(115);
                if (!save.TrySaveCurrentRun(out var equipmentSaveError))
                {
                    Fail($"could not create equipment MaxHp save: {equipmentSaveError}");
                    return;
                }

                if (!player.RestoreInventory(Array.Empty<Item>(), null))
                {
                    Fail("could not clear equipment before equipment restore");
                    return;
                }

                _stage = 3;
                return;

            case 3:
                if (!save.TryLoadAndApplyLastRun(out _, out var equipmentLoadError))
                {
                    Fail($"equipment MaxHp save could not load: {equipmentLoadError}");
                    return;
                }

                var equipmentRestorePass = player.MaxHealth == 125
                    && player.CurrentHealth == 115
                    && player.EquippedWeapon?.Id == "save_recovery_vitality_weapon";
                if (!equipmentRestorePass)
                {
                    Fail($"equipment MaxHp restore failed max={player.MaxHealth} hp={player.CurrentHealth} weapon={player.EquippedWeaponName}");
                    return;
                }

                player.ApplyDamage(new DamageRequest(
                    9999,
                    DamageType.Physical,
                    "save_recovery_smoke",
                    CombatFaction.Enemy));
                _stage = 4;
                return;

            case 4:
                if (flow.State != GameFlowState.GameOver || player.IsAlive)
                {
                    if (_elapsed > 8.0)
                    {
                        Fail($"death setup failed state={flow.State} alive={player.IsAlive}");
                    }

                    return;
                }

                if (!save.TryLoadAndApplyLastRun(out _, out var loadError))
                {
                    Fail($"Playing save could not restore after death: {loadError}");
                    return;
                }

                var runtimePass = player.IsAlive
                    && player.CurrentHealth > 0
                    && player.CollisionLayer == PlayerController3D.PlayerCollisionLayer
                    && player.CollisionMask == PlayerController3D.PlayerCollisionMask
                    && player.IsPhysicsProcessing()
                    && flow.State == GameFlowState.Playing
                    && !GetTree().Paused
                    && player.RewardStats.EquivalentTo(Stats.Neutral)
                    && player.EquippedWeapon?.Id == "save_recovery_vitality_weapon";
                if (!runtimePass)
                {
                    Fail($"runtime restore failed hp={player.CurrentHealth} layer={player.CollisionLayer} mask={player.CollisionMask} physics={player.IsPhysicsProcessing()} state={flow.State} paused={GetTree().Paused}");
                    return;
                }

                player.SetAimDirectionForTest(Vector3.Right);
                var positionBeforeDash = player.GlobalPosition;
                var dashPass = player.PerformDash(0.8)
                    && player.GlobalPosition.DistanceSquaredTo(positionBeforeDash) > 0.0001f;
                var castPass = player.CastSpreadShot();
                var healthBeforeDamage = player.CurrentHealth;
                var damageResult = player.ApplyDamage(new DamageRequest(
                    1,
                    DamageType.Physical,
                    "save_recovery_runtime_smoke",
                    CombatFaction.Enemy));
                var damagePass = damageResult.DamageApplied > 0
                    && player.CurrentHealth < healthBeforeDamage;
                if (!dashPass || !castPass || !damagePass)
                {
                    Fail($"restored runtime behavior failed dash={dashPass} cast={castPass} damage={damagePass}");
                    return;
                }

                _complete = true;
                GD.Print("SAVE_RECOVERY_3D_SPIKE_PASS atomic_rollback=true reward_max_hp=true equipment_max_hp=true playing_runtime=true movement=true cast=true damage=true collision_restored=true");
                GetTree().Quit();
                return;
        }

        if (_elapsed > 12.0)
        {
            Fail($"recovery timed out at stage {_stage}");
        }
    }

    private void Fail(string reason)
    {
        _complete = true;
        GD.PushError($"SAVE_RECOVERY_3D_SPIKE_FAIL {reason}");
        GetTree().Quit(1);
    }

    private static Item CreateMaxHpWeapon()
    {
        var baseDefinition = ItemBaseLibrary.Find("rustbound_blade");
        var vitalityAffix = new Affix
        {
            Id = "save_recovery_vitality",
            Name = "Vitality",
            Tier = 1,
            IsPrefix = true,
            Stats = new Stats { MaxHp = 25 },
        };

        var item = new Item
        {
            Id = "save_recovery_vitality_weapon",
            Name = "Save Recovery Vitality Blade",
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
