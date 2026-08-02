using System;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Small end-to-end contract for the mastery round: the supported skill
/// loadout, run-owned progression/passive stats, ailment runtime and the
/// existing save boundary must agree on one player state.
/// </summary>
public partial class MasteryCombatDepth3DRegressionSmoke : Node
{
    private double _elapsed;
    private bool _complete;
    private RunSessionNode _run;
    private TestArena3D _arena;
    private PlayerController3D _player;
    private PlayerSkillController3D _skills;
    private SaveBoundaryNode3D _save;
    private int _savedLevel;
    private int _savedDamage;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_complete)
        {
            return;
        }

        BindRuntime();
        if (_run == null || _arena == null || _player == null || _skills == null || _save == null)
        {
            if (_elapsed > 12.0)
            {
                Fail("mastery depth smoke runtime did not become ready");
            }

            return;
        }

        try
        {
            RunContract();
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    private void BindRuntime()
    {
        _run ??= GetNodeOrNull<RunSessionNode>("RunShell3D");
        _arena ??= GetTree().GetNodesInGroup("arena_3d")
            .OfType<TestArena3D>()
            .FirstOrDefault(map => map.UsesEncounterRuntime);
        _player ??= _arena?.GetNodeOrNull<PlayerController3D>("Player3D");
        _skills ??= _player?.GetNodeOrNull<PlayerSkillController3D>("PlayerSkillController3D");
        _save ??= _arena?.GetNodeOrNull<SaveBoundaryNode3D>("SaveBoundary3D");
        var director = _arena?.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        if (director != null)
        {
            director.Enabled = false;
            director.ProcessMode = ProcessModeEnum.Disabled;
        }
    }

    private void RunContract()
    {
        var support = _skills.Supports(SkillSlot.Primary).SingleOrDefault();
        var supportPass = support?.Id == "volley"
            && _skills.SupportCount(SkillSlot.Primary) == 1
            && SkillSupportMath.ProjectileCount(
                _skills.Definition(SkillSlot.Primary),
                _player.EffectiveStats,
                _skills.Supports(SkillSlot.Primary)) == 5;
        if (!supportPass)
        {
            Fail($"supported skill loadout was not applied support={support?.Id} count={_skills.SupportCount(SkillSlot.Primary)}");
            return;
        }

        var baseDamage = _player.SpreadShotDamage;
        if (!_run.TryRestoreProgression(50, new[] { "sharpened-bolt" }))
        {
            Fail("run progression could not restore a legal mastery passive");
            return;
        }

        var progressionPass = _run.CharacterLevel >= 2
            && _run.TotalExperience == 50
            && _run.PassiveTree.IsAllocated("sharpened-bolt")
            && _run.PassiveStats.ProjectileDamageMultiplier > 1.0
            && _player.SpreadShotDamage > baseDamage;
        if (!progressionPass)
        {
            Fail($"progression/passive stats did not reach player damage level={_run.CharacterLevel} xp={_run.TotalExperience} damage={_player.SpreadShotDamage}/{baseDamage}");
            return;
        }

        var ailmentResult = _player.ApplyDamage(new DamageRequest(
            1,
            DamageType.Fire,
            "mastery_depth_burning",
            CombatFaction.Enemy,
            false,
            new AilmentApplicationDefinition(AilmentKind.Burning, 100)));
        var ailmentPass = ailmentResult.DamageApplied > 0
            && _player.Ailments.Collection.Has(AilmentKind.Burning);
        if (!ailmentPass)
        {
            Fail("mastery ailment request did not reach the player runtime");
            return;
        }

        if (!_save.TrySaveCurrentRun(out var saveError))
        {
            Fail($"mastery save could not be written: {saveError}");
            return;
        }

        _savedLevel = _run.CharacterLevel;
        _savedDamage = _player.SpreadShotDamage;
        if (!_run.TryRestoreProgression(0, Array.Empty<string>())
            || !_run.TryRestoreProgression(50, new[] { "sharpened-bolt" }))
        {
            Fail("mastery smoke could not mutate and rebuild progression before load");
            return;
        }

        if (!_save.TryLoadAndApplyLastRun(out _, out var loadError))
        {
            Fail($"mastery save could not restore: {loadError}");
            return;
        }

        var savePass = _run.CharacterLevel == _savedLevel
            && _run.TotalExperience == 50
            && _run.PassiveTree.IsAllocated("sharpened-bolt")
            && _player.SpreadShotDamage == _savedDamage
            && _skills.Supports(SkillSlot.Primary).SingleOrDefault()?.Id == "volley";
        if (!savePass)
        {
            Fail($"mastery save state was not restored level={_run.CharacterLevel}/{_savedLevel} damage={_player.SpreadShotDamage}/{_savedDamage}");
            return;
        }

        _complete = true;
        GD.Print("MASTERY_COMBAT_DEPTH_3D_REGRESSION_PASS support=true progression=true passive=true ailment=true save_restore=true damage_pipeline=true");
        GetTree().Quit();
    }

    private void Fail(string reason)
    {
        if (_complete)
        {
            return;
        }

        _complete = true;
        GD.PushError($"MASTERY_COMBAT_DEPTH_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}
