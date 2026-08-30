using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// Covers the product entry boundary: absent/malformed saves, fresh start,
/// plan-correct continuation, and a full fresh restart.
/// </summary>
public partial class RunEntry3DRegressionSmoke : Node
{
    private enum Stage
    {
        VerifyEmptyMenu,
        VerifyMalformedSave,
        StartFreshRun,
        SaveAdvancedRun,
        ReleaseFirstBootstrap,
        VerifyContinueMenu,
        ContinueRun,
        VerifyRestoredRun,
        RestartRun,
        VerifyRestartedRun,
    }

    private readonly MinimalSaveService _saveService = new();
    private Stage _stage;
    private double _elapsed;
    private bool _complete;
    private GameBootstrap3D _bootstrap;
    private PackedScene _bootstrapScene;
    private RunSessionNode _firstRun;
    private RunSessionNode _restoredRun;
    private RunSessionNode _restartedRun;

    public override void _EnterTree()
    {
        _saveService.Delete();
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _bootstrap = GetNodeOrNull<GameBootstrap3D>("GameBootstrap3D");
        _bootstrapScene = GD.Load<PackedScene>("res://scenes3d/GameBootstrap3D.tscn");
        if (_bootstrap == null || _bootstrapScene == null)
        {
            Fail("run entry smoke could not load the product bootstrap");
        }
    }

    public override void _Process(double delta)
    {
        if (_complete)
        {
            return;
        }

        _elapsed += delta;
        if (_elapsed > 36.0)
        {
            Fail($"run entry smoke timed out at stage {_stage}");
            return;
        }

        try
        {
            switch (_stage)
            {
                case Stage.VerifyEmptyMenu:
                    VerifyEmptyMenu();
                    break;
                case Stage.VerifyMalformedSave:
                    VerifyMalformedSave();
                    break;
                case Stage.StartFreshRun:
                    StartFreshRun();
                    break;
                case Stage.SaveAdvancedRun:
                    SaveAdvancedRun();
                    break;
                case Stage.ReleaseFirstBootstrap:
                    ReleaseFirstBootstrap();
                    break;
                case Stage.VerifyContinueMenu:
                    VerifyContinueMenu();
                    break;
                case Stage.ContinueRun:
                    ContinueRun();
                    break;
                case Stage.VerifyRestoredRun:
                    VerifyRestoredRun();
                    break;
                case Stage.RestartRun:
                    RestartRun();
                    break;
                case Stage.VerifyRestartedRun:
                    VerifyRestartedRun();
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    private void VerifyEmptyMenu()
    {
        if (!_bootstrap.IsMenuVisible || _bootstrap.IsContinueEnabled)
        {
            Fail("empty entry menu did not disable Continue");
            return;
        }

        using (var file = FileAccess.Open(MinimalSaveService.DefaultPath, FileAccess.ModeFlags.Write))
        {
            if (file == null)
            {
                Fail($"could not create malformed entry save: {FileAccess.GetOpenError()}");
                return;
            }
            file.StoreString("{ malformed startup save");
        }

        _bootstrap.RefreshSaveAvailability();
        _stage = Stage.VerifyMalformedSave;
        _elapsed = 0.0;
    }

    private void VerifyMalformedSave()
    {
        if (_bootstrap.IsContinueEnabled
            || !_bootstrap.StatusText.StartsWith("Save unavailable:", StringComparison.Ordinal))
        {
            Fail("malformed save was exposed as a valid Continue action");
            return;
        }

        _saveService.Delete();
        _bootstrap.RefreshSaveAvailability();
        _stage = Stage.StartFreshRun;
        _elapsed = 0.0;
    }

    private void StartFreshRun()
    {
        if (!_bootstrap.StartNewRun())
        {
            Fail("New Run action was rejected");
            return;
        }

        _stage = Stage.SaveAdvancedRun;
        _elapsed = 0.0;
    }

    private void SaveAdvancedRun()
    {
        _firstRun ??= _bootstrap.ActiveRun;
        var arena = _firstRun?.CurrentMap3D;
        var player = arena?.GetNodeOrNull<PlayerController3D>("Player3D");
        var save = arena?.GetNodeOrNull<SaveBoundaryNode3D>("SaveBoundary3D");
        if (_firstRun == null || arena == null || player == null || save == null)
        {
            return;
        }

        if (_bootstrap.IsMenuVisible
            || _firstRun.CurrentMapLevel != 1
            || arena.AppliedRunPlan?.MapLevel != 1)
        {
            Fail("New Run did not start a clean map-one plan");
            return;
        }

        var session = _firstRun.Session;
        var unlocked = new[]
        {
            "quiet-coast",
            "hardened-frontier",
            "volatile-rift",
            "brimstone-caldera",
        };
        var completed = new[] { "quiet-coast", "hardened-frontier" };
        if (!_firstRun.TryRestore(
                session.RunSeed,
                session.ItemSequence,
                4,
                session.LootRandom.State,
                session.CraftingRandom.State,
                session.EventRandom.State,
                "brimstone-caldera",
                string.Empty,
                unlocked,
                completed,
                2))
        {
            Fail("could not prepare the advanced startup fixture");
            return;
        }

        player.SetRewardStats(new Stats { MaxHp = 25, DamageMultiplier = 1.35 });
        if (!save.TrySaveCurrentRun(out var error))
        {
            Fail($"could not save the advanced startup fixture: {error}");
            return;
        }

        _bootstrap.QueueFree();
        _bootstrap = null;
        _stage = Stage.ReleaseFirstBootstrap;
        _elapsed = 0.0;
    }

    private void ReleaseFirstBootstrap()
    {
        if (GetTree().GetNodesInGroup("run_sessions").Count > 0)
        {
            return;
        }

        _bootstrap = _bootstrapScene.Instantiate<GameBootstrap3D>();
        _bootstrap.Name = "ContinuedBootstrap3D";
        AddChild(_bootstrap);
        _stage = Stage.VerifyContinueMenu;
        _elapsed = 0.0;
    }

    private void VerifyContinueMenu()
    {
        if (!_bootstrap.IsMenuVisible || !_bootstrap.IsContinueEnabled)
        {
            Fail("valid saved run was not exposed by the Continue menu");
            return;
        }

        _stage = Stage.ContinueRun;
        _elapsed = 0.0;
    }

    private void ContinueRun()
    {
        if (!_bootstrap.ContinueRun())
        {
            Fail($"Continue action was rejected: {_bootstrap.StatusText}");
            return;
        }

        _stage = Stage.VerifyRestoredRun;
        _elapsed = 0.0;
    }

    private void VerifyRestoredRun()
    {
        _restoredRun ??= _bootstrap.ActiveRun;
        var arena = _restoredRun?.CurrentMap3D;
        var player = arena?.GetNodeOrNull<PlayerController3D>("Player3D");
        if (_restoredRun == null || arena == null || player == null || _bootstrap.IsMenuVisible)
        {
            return;
        }

        if (_restoredRun.CurrentMapLevel != 4
            || _restoredRun.CurrentAtlasMapId != "brimstone-caldera"
            || arena.AppliedRunPlan?.MapLevel != 4
            || arena.AppliedRunPlan.AtlasMapId != "brimstone-caldera"
            || arena.AppliedRunPlan.EncounterId != "siege_pressure"
            || player.MaxHealth != 125
            || Math.Abs(player.RewardStats.DamageMultiplier - 1.35) > 0.001)
        {
            Fail(
                $"Continue restored the wrong target state level={_restoredRun.CurrentMapLevel} "
                + $"atlas={_restoredRun.CurrentAtlasMapId} plan={arena.AppliedRunPlan?.AtlasMapId} "
                + $"encounter={arena.AppliedRunPlan?.EncounterId} hp={player.MaxHealth} "
                + $"damage={player.RewardStats.DamageMultiplier}");
            return;
        }

        _stage = Stage.RestartRun;
        _elapsed = 0.0;
    }

    private void RestartRun()
    {
        _bootstrap.RestartRun();
        _stage = Stage.VerifyRestartedRun;
        _elapsed = 0.0;
    }

    private void VerifyRestartedRun()
    {
        _restartedRun = _bootstrap.ActiveRun;
        var arena = _restartedRun?.CurrentMap3D;
        if (_restartedRun == null || arena == null || _restartedRun == _restoredRun)
        {
            return;
        }

        if (_bootstrap.IsMenuVisible
            || _restartedRun.CurrentMapLevel != 1
            || _restartedRun.CurrentAtlasMapId != "quiet-coast"
            || arena.AppliedRunPlan?.MapLevel != 1
            || FileAccess.FileExists(MinimalSaveService.DefaultPath))
        {
            Fail("Restart did not replace the continued run with a clean map-one session");
            return;
        }

        _complete = true;
        _saveService.Delete();
        GD.Print(
            "RUN_ENTRY_3D_REGRESSION_PASS empty_disabled=true malformed_disabled=true "
            + "new_run=true continue_level=4 continue_plan=true restart_level=1");
        GetTree().Quit();
    }

    private void Fail(string reason)
    {
        if (_complete)
        {
            return;
        }

        _complete = true;
        GetTree().Paused = false;
        _saveService.Delete();
        GD.PushError($"RUN_ENTRY_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}
