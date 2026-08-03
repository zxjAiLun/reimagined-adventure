using System;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Proves that the awarded-experience identity ledger survives releasing one
/// RunShell and loading the same Playing save into a fresh RunSession.
/// </summary>
public partial class FreshSessionExperience3DRegressionSmoke : Node
{
    private enum Stage
    {
        WaitFirstEnemy,
        WaitFirstDeath,
        SavePlaying,
        ReleaseFirstShell,
        ApplyFreshSave,
        WaitDuplicate,
        WaitDuplicateDeath,
        WaitNewIdentity,
        WaitNewDeath,
    }

    private readonly MinimalSaveService _saveService = new();
    private Stage _stage;
    private double _elapsed;
    private bool _complete;
    private PackedScene _shellScene;
    private Node _firstShell;
    private Node _freshShell;
    private TestArena3D _firstArena;
    private TestArena3D _freshArena;
    private RunSessionNode _firstRun;
    private RunSessionNode _freshRun;
    private FeralController3D _firstEnemy;
    private FeralController3D _duplicateEnemy;
    private FeralController3D _newEnemy;
    private string _awardedSourceId = string.Empty;
    private int _experienceAfterFirstKill;
    private int _experienceBeforeDuplicate;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _saveService.Delete();
        _shellScene = GD.Load<PackedScene>("res://scenes3d/RunShell3D.tscn");
        _firstShell = GetNodeOrNull<Node>("RunShell3D");
        if (_shellScene == null || _firstShell == null)
        {
            Fail("fresh-session smoke could not bind its initial RunShell");
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
            Fail($"fresh-session XP smoke timed out at stage {_stage}");
            return;
        }

        try
        {
            switch (_stage)
            {
                case Stage.WaitFirstEnemy:
                    WaitForFirstEnemy();
                    break;
                case Stage.WaitFirstDeath:
                    WaitForFirstDeath();
                    break;
                case Stage.SavePlaying:
                    SavePlayingState();
                    break;
                case Stage.ReleaseFirstShell:
                    ReleaseFirstShell();
                    break;
                case Stage.ApplyFreshSave:
                    ApplyFreshSave();
                    break;
                case Stage.WaitDuplicate:
                    WaitForDuplicateIdentity();
                    break;
                case Stage.WaitDuplicateDeath:
                    WaitForDuplicateDeath();
                    break;
                case Stage.WaitNewIdentity:
                    WaitForNewIdentity();
                    break;
                case Stage.WaitNewDeath:
                    WaitForNewDeath();
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    private void WaitForFirstEnemy()
    {
        _firstArena ??= _firstShell?.GetNodeOrNull<TestArena3D>("TestArena3D");
        _firstRun ??= _firstShell as RunSessionNode;
        _firstEnemy ??= FindFerals(_firstArena)
            .FirstOrDefault(enemy => enemy.IsAlive);
        if (_firstArena == null || _firstRun == null || _firstEnemy == null)
        {
            return;
        }

        _awardedSourceId = SourceId(_firstRun, _firstEnemy);
        var director = _firstArena.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        if (director != null)
        {
            director.Enabled = false;
            director.ProcessMode = ProcessModeEnum.Disabled;
        }

        _firstEnemy.ApplyDamage(new DamageRequest(
            999999,
            DamageType.Physical,
            "fresh_session_first_kill",
            CombatFaction.Player));
        _stage = Stage.WaitFirstDeath;
        _elapsed = 0.0;
    }

    private void WaitForFirstDeath()
    {
        if (_firstEnemy?.IsAlive == true)
        {
            return;
        }

        if (_firstRun == null
            || !_firstRun.AwardedExperienceSourceIds.Contains(_awardedSourceId)
            || _firstRun.TotalExperience <= 0)
        {
            Fail("first formal enemy death did not record XP identity");
            return;
        }

        _experienceAfterFirstKill = _firstRun.TotalExperience;
        _stage = Stage.SavePlaying;
        _elapsed = 0.0;
    }

    private void SavePlayingState()
    {
        var save = _firstArena?.GetNodeOrNull<SaveBoundaryNode3D>("SaveBoundary3D");
        var error = string.Empty;
        if (save == null || !save.TrySaveCurrentRun(out error))
        {
            Fail($"could not save fresh-session Playing state: {error}");
            return;
        }

        if (!_saveService.TryLoad(out var state, out error)
            || !state.AwardedExperienceSourceIds.Contains(_awardedSourceId))
        {
            Fail($"saved XP identity ledger was missing: {error}");
            return;
        }

        _stage = Stage.ReleaseFirstShell;
        _elapsed = 0.0;
    }

    private void ReleaseFirstShell()
    {
        if (_firstShell != null && GodotObject.IsInstanceValid(_firstShell))
        {
            _firstShell.QueueFree();
            _firstShell = null;
            return;
        }

        if (GetTree().GetNodesInGroup("run_sessions").Count > 0)
        {
            return;
        }

        _freshShell = _shellScene.Instantiate<Node>();
        _freshShell.Name = "FreshRunShell3D";
        AddChild(_freshShell);
        _stage = Stage.ApplyFreshSave;
        _elapsed = 0.0;
    }

    private void ApplyFreshSave()
    {
        _freshArena ??= _freshShell?.GetNodeOrNull<TestArena3D>("TestArena3D");
        _freshRun ??= _freshShell as RunSessionNode;
        var save = _freshArena?.GetNodeOrNull<SaveBoundaryNode3D>("SaveBoundary3D");
        if (_freshArena == null || _freshRun == null || save == null)
        {
            return;
        }

        if (!save.TryLoadAndApplyLastRun(out var state, out var error))
        {
            Fail($"fresh RunSession could not apply Playing save: {error}");
            return;
        }

        if (state.TotalExperience != _experienceAfterFirstKill
            || !_freshRun.AwardedExperienceSourceIds.Contains(_awardedSourceId))
        {
            Fail("fresh RunSession did not restore progression identity ledger");
            return;
        }

        _stage = Stage.WaitDuplicate;
        _elapsed = 0.0;
    }

    private void WaitForDuplicateIdentity()
    {
        _duplicateEnemy ??= FindFerals(_freshArena)
            .FirstOrDefault(enemy => enemy.IsAlive && SourceId(_freshRun, enemy) == _awardedSourceId);
        if (_duplicateEnemy == null)
        {
            return;
        }

        _experienceBeforeDuplicate = _freshRun.TotalExperience;
        _duplicateEnemy.ApplyDamage(new DamageRequest(
            999999,
            DamageType.Physical,
            "fresh_session_duplicate_kill",
            CombatFaction.Player));
        _stage = Stage.WaitDuplicateDeath;
        _elapsed = 0.0;
    }

    private void WaitForDuplicateDeath()
    {
        if (_duplicateEnemy?.IsAlive == true)
        {
            return;
        }

        if (_freshRun.TotalExperience != _experienceBeforeDuplicate)
        {
            Fail($"duplicate formal enemy awarded XP after fresh restore {_experienceBeforeDuplicate}->{_freshRun.TotalExperience}");
            return;
        }

        _stage = Stage.WaitNewIdentity;
        _elapsed = 0.0;
    }

    private void WaitForNewIdentity()
    {
        _newEnemy ??= FindFerals(_freshArena)
            .FirstOrDefault(enemy => enemy.IsAlive && SourceId(_freshRun, enemy) != _awardedSourceId);
        if (_newEnemy == null)
        {
            return;
        }

        var director = _freshArena.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        if (director != null)
        {
            director.Enabled = false;
            director.ProcessMode = ProcessModeEnum.Disabled;
        }

        _newEnemy.ApplyDamage(new DamageRequest(
            999999,
            DamageType.Physical,
            "fresh_session_new_identity_kill",
            CombatFaction.Player));
        _stage = Stage.WaitNewDeath;
        _elapsed = 0.0;
    }

    private void WaitForNewDeath()
    {
        if (_newEnemy?.IsAlive == true)
        {
            return;
        }

        var expectedGain = _newEnemy.EliteModifier == null
            ? ExperienceRewards.BaseExperience(ExperienceSourceKind.Feral)
            : ExperienceRewards.EliteExperience(ExperienceSourceKind.Feral);
        if (_freshRun.TotalExperience != _experienceBeforeDuplicate + expectedGain)
        {
            Fail($"new formal enemy XP mismatch expected={_experienceBeforeDuplicate + expectedGain} actual={_freshRun.TotalExperience}");
            return;
        }

        _complete = true;
        _saveService.Delete();
        GD.Print(
            $"FRESH_SESSION_EXPERIENCE_3D_REGRESSION_PASS ledger=true duplicate_blocked=true "
            + $"new_identity_awarded=true xp={_freshRun.TotalExperience}");
        GetTree().Quit();
    }

    private static FeralController3D[] FindFerals(TestArena3D arena) =>
        arena?.GetNodeOrNull<Node3D>("EnemyContainer")?.GetChildren()
            .OfType<FeralController3D>()
            .ToArray()
        ?? Array.Empty<FeralController3D>();

    private static string SourceId(RunSessionNode run, FeralController3D enemy) =>
        string.IsNullOrWhiteSpace(enemy.SpawnEncounterId)
            ? $"feral:map-{run.CurrentMapLevel}:{enemy.GetPath()}"
            : $"feral:map-{run.CurrentMapLevel}:{enemy.SpawnEncounterId}:{enemy.SpawnWaveId}:{enemy.SpawnOrdinal}";

    private void Fail(string reason)
    {
        if (_complete)
        {
            return;
        }

        _complete = true;
        _saveService.Delete();
        GD.PushError($"FRESH_SESSION_EXPERIENCE_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}
