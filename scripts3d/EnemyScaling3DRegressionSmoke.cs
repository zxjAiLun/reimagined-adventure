using System;
using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Exercises the production pre-ready spawn configuration at map levels 1, 2
/// and 4. The smoke deliberately reuses the outer RunSession and local map
/// Player rather than calling actor _Ready methods or mutating shared scenes.
/// </summary>
public partial class EnemyScaling3DRegressionSmoke : Node
{
    private readonly Dictionary<int, int> _feralHealth = new();
    private readonly Dictionary<int, int> _spitterHealth = new();
    private readonly Dictionary<int, int> _bossHealth = new();
    private readonly Dictionary<int, int> _feralDamage = new();
    private readonly Dictionary<int, int> _spitterDamage = new();
    private readonly Dictionary<int, int> _bossSlamDamage = new();
    private readonly Dictionary<int, int> _bossSpearDamage = new();
    private readonly Dictionary<int, int> _dropLevels = new();
    private RunSessionNode _run;
    private TestArena3D _arena;
    private PlayerController3D _player;
    private Node3D _enemyContainer;
    private bool _complete;
    private int _nextLevelIndex;
    private float _elapsed;
    private float _feralMoveSpeed;
    private float _spitterMoveSpeed;
    private float _bossMoveSpeed;
    private float _feralWindup;
    private float _feralRecovery;
    private float _spitterAim;
    private float _spitterTelegraph;
    private float _spitterRecovery;
    private float _bossSlamPreparation;
    private float _bossSpearPreparation;
    private float _bossRecovery;

    private static readonly int[] Levels = [1, 2, 4];

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

        _elapsed += (float)delta;
        BindRuntime();
        if (_run == null || _arena == null || _player == null || _enemyContainer == null)
        {
            if (_elapsed > 10.0f)
            {
                Fail("scaling smoke runtime did not become ready");
            }

            return;
        }

        var director = _arena.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        if (director != null)
        {
            director.Enabled = false;
            director.ProcessMode = ProcessModeEnum.Disabled;
        }

        if (_nextLevelIndex >= Levels.Length)
        {
            CompleteSmoke();
            return;
        }

        var mapLevel = Levels[_nextLevelIndex++];
        if (!RestoreMapLevel(mapLevel))
        {
            return;
        }

        if (!VerifyLevel(mapLevel))
        {
            return;
        }
    }

    private void BindRuntime()
    {
        _run ??= GetNodeOrNull<RunSessionNode>("RunShell3D");
        _arena ??= GetTree().GetNodesInGroup("arena_3d")
            .OfType<TestArena3D>()
            .LastOrDefault(map => map.UsesEncounterRuntime);
        _player ??= _arena?.GetNodeOrNull<PlayerController3D>("Player3D");
        _enemyContainer ??= _arena?.GetNodeOrNull<Node3D>("EnemyContainer");
    }

    private bool RestoreMapLevel(int mapLevel)
    {
        if (_run.CurrentMapLevel == mapLevel)
        {
            return true;
        }

        var session = _run.Session;
        if (!_run.TryRestore(
                session.RunSeed,
                session.ItemSequence,
                mapLevel,
                session.LootRandom.State,
                session.CraftingRandom.State,
                session.EventRandom.State))
        {
            Fail($"could not set smoke map level to {mapLevel}");
            return false;
        }

        return true;
    }

    private bool VerifyLevel(int mapLevel)
    {
        var modifier = new MapModifierStats();
        var expectedDropLevel = MapScaling.ItemLevel(mapLevel, modifier);
        var feral = Spawn<FeralController3D>(
            "res://scenes3d/Feral3D.tscn",
            mapLevel,
            1,
            false,
            new Vector3(-8.0f, 0.0f, -5.0f));
        var spitter = Spawn<SpitterController3D>(
            "res://scenes3d/Spitter3D.tscn",
            mapLevel,
            1,
            false,
            new Vector3(8.0f, 0.0f, 5.0f));
        var boss = Spawn<BrimstoneColossusController3D>(
            "res://scenes3d/BrimstoneColossus3D.tscn",
            mapLevel,
            2,
            true,
            new Vector3(6.0f, 0.0f, -6.0f));
        if (feral == null || spitter == null || boss == null)
        {
            return false;
        }

        if (feral.CurrentHealth != feral.AppliedMaxHealth
            || spitter.CurrentHealth != spitter.AppliedMaxHealth
            || boss.CurrentHealth != boss.AppliedMaxHealth)
        {
            Fail($"map {mapLevel} health was not initialized from pre-ready values");
            return false;
        }

        _feralHealth[mapLevel] = feral.AppliedMaxHealth;
        _spitterHealth[mapLevel] = spitter.AppliedMaxHealth;
        _bossHealth[mapLevel] = boss.AppliedMaxHealth;
        _feralDamage[mapLevel] = feral.AppliedPrimaryDamage;
        _spitterDamage[mapLevel] = spitter.AppliedPrimaryDamage;
        _bossSlamDamage[mapLevel] = boss.AppliedPrimaryDamage;
        _bossSpearDamage[mapLevel] = boss.AppliedSecondaryDamage;
        _dropLevels[mapLevel] = feral.AppliedDropItemLevel;

        if (feral.AppliedMapLevel != mapLevel
            || spitter.AppliedMapLevel != mapLevel
            || boss.AppliedMapLevel != mapLevel
            || feral.AppliedDropItemLevel != expectedDropLevel
            || spitter.AppliedDropItemLevel != expectedDropLevel
            || boss.AppliedDropItemLevel != expectedDropLevel
            || feral.SpawnContextAppliedCount != 1
            || spitter.SpawnContextAppliedCount != 1
            || boss.SpawnContextAppliedCount != 1
            || !ReferenceEquals(feral.RunSession, _run)
            || !ReferenceEquals(spitter.RunSession, _run)
            || !ReferenceEquals(boss.RunSession, _run)
            || !ReferenceEquals(feral.TargetPlayer, _player)
            || !ReferenceEquals(spitter.TargetPlayer, _player)
            || !ReferenceEquals(boss.TargetPlayer, _player)
            || feral.GetNode<NavigationAgent3D>("NavigationAgent3D").NavigationLayers != 1
            || spitter.GetNode<NavigationAgent3D>("NavigationAgent3D").NavigationLayers != 1
            || boss.GetNode<NavigationAgent3D>("NavigationAgent3D").NavigationLayers != 2)
        {
            Fail($"map {mapLevel} spawn context diagnostics were invalid");
            return false;
        }

        if (mapLevel == 1)
        {
            _feralMoveSpeed = feral.MoveSpeed;
            _spitterMoveSpeed = spitter.MoveSpeed;
            _bossMoveSpeed = boss.MoveSpeed;
            _feralWindup = feral.AttackWindupSeconds;
            _feralRecovery = feral.AttackRecoverySeconds;
            _spitterAim = spitter.AimSeconds;
            _spitterTelegraph = spitter.TelegraphSeconds;
            _spitterRecovery = spitter.RecoverySeconds;
            _bossSlamPreparation = boss.MagmaSlamPreparationSeconds;
            _bossSpearPreparation = boss.FlameSpearPreparationSeconds;
            _bossRecovery = boss.RecoverySeconds;
        }
        else if (feral.MoveSpeed != _feralMoveSpeed
            || spitter.MoveSpeed != _spitterMoveSpeed
            || boss.MoveSpeed != _bossMoveSpeed
            || feral.AttackWindupSeconds != _feralWindup
            || feral.AttackRecoverySeconds != _feralRecovery
            || spitter.AimSeconds != _spitterAim
            || spitter.TelegraphSeconds != _spitterTelegraph
            || spitter.RecoverySeconds != _spitterRecovery
            || boss.MagmaSlamPreparationSeconds != _bossSlamPreparation
            || boss.FlameSpearPreparationSeconds != _bossSpearPreparation
            || boss.RecoverySeconds != _bossRecovery)
        {
            Fail($"map {mapLevel} changed movement or attack timing configuration");
            return false;
        }

        foreach (var enemy in new Node3D[] { feral, spitter, boss })
        {
            enemy.SetPhysicsProcess(false);
        }

        return true;
    }

    private T Spawn<T>(string scenePath, int mapLevel, int navigationLayers, bool isBoss, Vector3 position)
        where T : Node3D, IEnemySpawnConfigurable3D
    {
        var scene = GD.Load<PackedScene>(scenePath);
        if (scene == null)
        {
            Fail($"could not load {scenePath}");
            return null;
        }

        var enemy = scene.Instantiate<T>();
        var context = new EnemySpawnContext3D(
            _run,
            _player,
            mapLevel,
            "scaling_smoke",
            $"map_{mapLevel}",
            _enemyContainer.GetChildCount() + 1,
            navigationLayers,
            new MapModifierStats(),
            MapScaling.ItemLevel(mapLevel, new MapModifierStats()),
            isBoss);
        try
        {
            context.Validate();
            enemy.ConfigureBeforeReady(context);
            _enemyContainer.AddChild(enemy);
            enemy.GlobalPosition = position;
            return enemy;
        }
        catch (Exception exception)
        {
            Fail($"spawn context failed for {scenePath}: {exception.Message}");
            enemy.QueueFree();
            return null;
        }
    }

    private void CompleteSmoke()
    {
        if (_feralHealth.GetValueOrDefault(1) != 40
            || _feralHealth.GetValueOrDefault(2) != 50
            || _feralHealth.GetValueOrDefault(4) != 70
            || _spitterHealth.GetValueOrDefault(1) != 24
            || _spitterHealth.GetValueOrDefault(2) != 30
            || _spitterHealth.GetValueOrDefault(4) != 42
            || _bossHealth.GetValueOrDefault(1) != 160
            || _bossHealth.GetValueOrDefault(2) != 200
            || _bossHealth.GetValueOrDefault(4) != 280
            || _feralDamage.GetValueOrDefault(4) != _feralDamage.GetValueOrDefault(1) + 1
            || _spitterDamage.GetValueOrDefault(4) != _spitterDamage.GetValueOrDefault(1) + 1
            || _bossSlamDamage.GetValueOrDefault(4) != _bossSlamDamage.GetValueOrDefault(1) + 1
            || _bossSpearDamage.GetValueOrDefault(4) != _bossSpearDamage.GetValueOrDefault(1) + 1
            || _dropLevels.GetValueOrDefault(1) != 1
            || _dropLevels.GetValueOrDefault(2) != 2
            || _dropLevels.GetValueOrDefault(4) != 4)
        {
            Fail("MapScaling values did not match the 1/2/4 contract");
            return;
        }

        _complete = true;
        GD.Print("ENEMY_SCALING_3D_REGRESSION_PASS hp=true damage=true drops=true context_once=true resource_isolated=true");
        GetTree().Quit();
    }

    private void Fail(string reason)
    {
        if (_complete)
        {
            return;
        }

        _complete = true;
        GD.PushError($"ENEMY_SCALING_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}
