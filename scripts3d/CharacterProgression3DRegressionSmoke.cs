using System;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Stage 15 smoke. It kills the real default encounter enemies, verifies
/// source-identity XP de-duplication, enters the formal build phase, allocates
/// a passive through keyboard input, and round-trips the run-owned state.
/// </summary>
public partial class CharacterProgression3DRegressionSmoke : Node
{
    private double _elapsed;
    private int _stage;
    private bool _complete;
    private RunSessionNode _run;
    private TestArena3D _arena;
    private EncounterDirector3D _director;
    private GameFlowController3D _flow;
    private MapRewardNode3D _rewards;
    private BuildIntermissionController3D _build;
    private SaveBoundaryNode3D _save;
    private PlayerController3D _player;
    private InventoryScreenController3D _screen;
    private int _experienceAfterFirstKill;
    private int _experienceBeforeBuild;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        if (_complete)
        {
            return;
        }

        _elapsed += delta;
        if (_elapsed > 20.0)
        {
            Fail($"character progression smoke timed out at stage {_stage}");
            return;
        }

        try
        {
            switch (_stage)
            {
                case 0:
                    TryBindRuntime();
                    break;
                case 1:
                    KillFirstWave();
                    break;
                case 2:
                    KillSecondWave();
                    break;
                case 3:
                    KillBossWave();
                    break;
                case 4:
                    EnterBuildAndAllocatePassive();
                    break;
                case 5:
                    VerifySaveRecovery();
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    private void TryBindRuntime()
    {
        _run = GetNodeOrNull<RunSessionNode>("RunShell3D");
        _arena = GetNodeOrNull<TestArena3D>("RunShell3D/TestArena3D");
        _director = _arena?.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        _flow = _arena?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        _rewards = _arena?.GetNodeOrNull<MapRewardNode3D>("MapRewards3D");
        _build = _arena?.GetNodeOrNull<BuildIntermissionController3D>("BuildIntermission3D");
        _save = _arena?.GetNodeOrNull<SaveBoundaryNode3D>("SaveBoundary3D");
        _player = _arena?.GetNodeOrNull<PlayerController3D>("Player3D");
        _screen = _arena?.GetNodeOrNull<InventoryScreenController3D>("Player3D/InventoryScreen3D");
        if (_run == null || _arena == null || _director == null || _flow == null
            || _rewards == null || _build == null || _save == null || _player == null || _screen == null
            || !_director.IsOperational)
        {
            return;
        }

        _director.ProcessMode = ProcessModeEnum.Pausable;
        _elapsed = 0.0;
        _stage = 1;
    }

    private void KillFirstWave()
    {
        if (_director.CurrentWaveIndex < 0 || _director.GetActiveEnemies().Count == 0)
        {
            return;
        }

        var enemies = _director.GetActiveEnemies().ToArray();
        var first = enemies[0];
        KillEnemy(first);
        _experienceAfterFirstKill = _run.TotalExperience;
        if (_experienceAfterFirstKill != ExperienceRewards.BaseExperience(ExperienceSourceKind.Feral))
        {
            throw new InvalidOperationException($"first Feral did not grant exactly 4 XP: {_experienceAfterFirstKill}");
        }

        KillEnemy(first);
        if (_run.TotalExperience != _experienceAfterFirstKill)
        {
            throw new InvalidOperationException("repeated Feral death callback awarded XP twice");
        }

        foreach (var enemy in enemies.Skip(1))
        {
            KillEnemy(enemy);
        }

        _elapsed = 0.0;
        _stage = 2;
    }

    private void KillSecondWave()
    {
        if (_director.CurrentWaveIndex < 1 || _director.GetActiveEnemies().Count == 0)
        {
            return;
        }

        foreach (var enemy in _director.GetActiveEnemies().ToArray())
        {
            KillEnemy(enemy);
        }

        _elapsed = 0.0;
        _stage = 3;
    }

    private void KillBossWave()
    {
        if (_director.CurrentWaveIndex < 2 || _director.GetActiveEnemies().Count == 0)
        {
            return;
        }

        var boss = _director.GetActiveEnemies().OfType<BrimstoneColossusController3D>().FirstOrDefault();
        if (boss == null)
        {
            return;
        }

        KillEnemy(boss);
        _experienceBeforeBuild = _run.TotalExperience;
        if (_experienceBeforeBuild != 72 || _run.CharacterLevel != 2)
        {
            throw new InvalidOperationException(
                $"real encounter XP progression mismatch: xp={_experienceBeforeBuild} level={_run.CharacterLevel}");
        }

        _elapsed = 0.0;
        _stage = 4;
    }

    private void EnterBuildAndAllocatePassive()
    {
        if (_flow.State != GameFlowState.MapComplete || !_rewards.HasChosen && _elapsed < 0.5)
        {
            if (_flow.State == GameFlowState.MapComplete && !_rewards.HasChosen)
            {
                _rewards.BeginChoice();
                _rewards.TryChooseReward(0);
            }

            return;
        }

        if (!_rewards.HasChosen)
        {
            _rewards.BeginChoice();
            if (!_rewards.TryChooseReward(0))
            {
                return;
            }
        }

        var playerBuild = _player.GetNode<PlayerBuildController3D>("PlayerBuildController3D");
        if (!_build.IsBuildManagement || !playerBuild.IsOpen)
        {
            return;
        }

        SendKey(Key.V);
        SendKey(Key.G);
        if (playerBuild.PanelMode != BuildPanelMode.Passives
            || !_run.PassiveTree.IsAllocated("sharpened-bolt")
            || _run.UnspentPassivePoints != 0
            || _player.PassiveStats.ProjectileDamageMultiplier <= 1.0
            || !_screen.PassiveText.Contains("Sharpened Bolt", StringComparison.Ordinal)
            || !_screen.PassiveText.Contains("Passive Points: 0", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("formal passive build input did not update runtime and UI");
        }

        if (!_save.TrySaveCurrentRun(out var saveError))
        {
            throw new InvalidOperationException($"could not save progression smoke state: {saveError}");
        }

        _run.TryRestoreProgression(0, Array.Empty<string>());
        _elapsed = 0.0;
        _stage = 5;
    }

    private void VerifySaveRecovery()
    {
        if (!_save.TryLoadAndApplyLastRun(out var restored, out var error))
        {
            throw new InvalidOperationException($"progression save recovery failed: {error}");
        }

        if (restored.TotalExperience != _experienceBeforeBuild
            || restored.AllocatedPassiveNodeIds.Count != 1
            || restored.AllocatedPassiveNodeIds[0] != "sharpened-bolt"
            || _run.TotalExperience != _experienceBeforeBuild
            || !_run.PassiveTree.IsAllocated("sharpened-bolt")
            || _player.PassiveStats.ProjectileDamageMultiplier <= 1.0
            || _player.CurrentHealth != _player.MaxHealth)
        {
            throw new InvalidOperationException("progression XP/passive save recovery was not atomic or complete");
        }

        _complete = true;
        GD.Print($"CHARACTER_PROGRESSION_3D_REGRESSION_PASS xp={_run.TotalExperience} level={_run.CharacterLevel} passive=sharpened-bolt");
        GetTree().Quit(0);
    }

    private void KillEnemy(Node3D enemy)
    {
        enemy.ProcessMode = ProcessModeEnum.Disabled;
        var target = enemy as ICombatTarget;
        if (target == null)
        {
            throw new InvalidOperationException($"encounter enemy {enemy.Name} is not damageable");
        }

        target.ApplyDamage(new DamageRequest(
            9999,
            DamageType.Physical,
            "character_progression_smoke",
            CombatFaction.Player));
    }

    private void SendKey(Key key)
    {
        var input = new InputEventKey
        {
            Keycode = key,
            PhysicalKeycode = key,
            Pressed = true,
        };
        GetViewport().PushInput(input);
    }

    private void Fail(string message)
    {
        if (_complete)
        {
            return;
        }

        _complete = true;
        GD.PushError($"CHARACTER_PROGRESSION_3D_REGRESSION_FAIL {message}");
        GetTree().Quit(1);
    }
}
