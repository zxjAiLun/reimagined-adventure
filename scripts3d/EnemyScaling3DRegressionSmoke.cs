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
    private readonly Dictionary<int, int> _actualDropCounts = new();
    private readonly HashSet<string> _seenDropIds = new(StringComparer.Ordinal);
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
    private FeralController3D _combatFeral;
    private SpitterController3D _combatSpitter;
    private BrimstoneColossusController3D _combatBoss;
    private int _combatLevel;
    private int _combatPhase;
    private float _combatElapsed;
    private readonly HashSet<int> _actualCombatLevels = new();

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

        if (_combatPhase != 0)
        {
            TickActualCombat((float)delta);
            return;
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

        if (mapLevel == 1 || mapLevel == 4)
        {
            BeginActualCombat(mapLevel, feral, spitter, boss);
            return true;
        }

        FinishLevelWithDrops(mapLevel, feral, spitter, boss, expectedDropLevel);
        return true;
    }

    private void BeginActualCombat(
        int mapLevel,
        FeralController3D feral,
        SpitterController3D spitter,
        BrimstoneColossusController3D boss)
    {
        _combatLevel = mapLevel;
        _combatPhase = 1;
        _combatElapsed = 0.0f;
        _combatFeral = feral;
        _combatSpitter = spitter;
        _combatBoss = boss;
        _player.ApplyRestoredHealth(_player.MaxHealth);
        _player.GlobalPosition = Vector3.Zero;
        feral.GlobalPosition = new Vector3(1.0f, 0.0f, 0.0f);
        spitter.GlobalPosition = new Vector3(0.0f, 0.0f, 5.0f);
        boss.GlobalPosition = new Vector3(2.0f, 0.0f, 0.0f);
        feral.SetPhysicsProcess(true);
    }

    private void TickActualCombat(float delta)
    {
        _combatElapsed += delta;
        if (_combatElapsed > 5.0f)
        {
            Fail($"map {_combatLevel} actual combat phase {_combatPhase} timed out");
            return;
        }

        switch (_combatPhase)
        {
            case 1:
                if (_combatFeral.ImpactCount > 0)
                {
                    if (_player.MaxHealth - _player.CurrentHealth != _combatFeral.AppliedPrimaryDamage)
                    {
                        Fail($"map {_combatLevel} Feral actual damage was {_player.MaxHealth - _player.CurrentHealth}, expected {_combatFeral.AppliedPrimaryDamage}");
                        return;
                    }

                    _combatFeral.SetPhysicsProcess(false);
                    _player.ApplyRestoredHealth(_player.MaxHealth);
                    _combatElapsed = 0.0f;
                    _combatPhase = 2;
                    _combatSpitter.SetPhysicsProcess(true);
                }

                break;
            case 2:
                if (_combatSpitter.ProjectileShotCount > 0
                    && _player.CurrentHealth < _player.MaxHealth)
                {
                    if (_player.MaxHealth - _player.CurrentHealth != _combatSpitter.AppliedPrimaryDamage)
                    {
                        Fail($"map {_combatLevel} Spitter actual damage was {_player.MaxHealth - _player.CurrentHealth}, expected {_combatSpitter.AppliedPrimaryDamage}");
                        return;
                    }

                    _combatSpitter.SetPhysicsProcess(false);
                    _player.ApplyRestoredHealth(_player.MaxHealth);
                    _combatElapsed = 0.0f;
                    _combatPhase = 3;
                    _combatBoss.SetPhysicsProcess(true);
                }

                break;
            case 3:
                if (_combatBoss.MagmaSlamImpactCount > 0
                    && _player.CurrentHealth < _player.MaxHealth)
                {
                    if (_player.MaxHealth - _player.CurrentHealth != _combatBoss.AppliedPrimaryDamage)
                    {
                        Fail($"map {_combatLevel} Boss actual damage was {_player.MaxHealth - _player.CurrentHealth}, expected {_combatBoss.AppliedPrimaryDamage}");
                        return;
                    }

                    _combatBoss.SetPhysicsProcess(false);
                    FinishLevelWithDrops(
                        _combatLevel,
                        _combatFeral,
                        _combatSpitter,
                        _combatBoss,
                        _combatFeral.AppliedDropItemLevel);
                    _actualCombatLevels.Add(_combatLevel);
                    _combatFeral = null;
                    _combatSpitter = null;
                    _combatBoss = null;
                    _combatPhase = 0;
                    _combatElapsed = 0.0f;
                }
                break;
        }
    }

    private void FinishLevelWithDrops(
        int mapLevel,
        FeralController3D feral,
        SpitterController3D spitter,
        BrimstoneColossusController3D boss,
        int expectedDropLevel)
    {
        foreach (var enemy in new ICombatTarget[] { feral, spitter, boss })
        {
            if (enemy.IsAlive)
            {
                enemy.ApplyDamage(new DamageRequest(
                    999999,
                    DamageType.Physical,
                    $"enemy_scaling_drop_map_{mapLevel}",
                    CombatFaction.Player));
            }
        }

        var actualDropCount = 0;
        foreach (var node in GetTree().GetNodesInGroup("item_drops_3d"))
        {
            if (node is not ItemDrop3D drop || drop.Item == null || !_seenDropIds.Add(drop.Item.Id))
            {
                continue;
            }

            actualDropCount++;
            if (drop.Item.ItemLevel != expectedDropLevel)
            {
                Fail($"map {mapLevel} actual drop item level was {drop.Item.ItemLevel}, expected {expectedDropLevel}");
                return;
            }
        }

        _actualDropCounts[mapLevel] = actualDropCount;
        if (actualDropCount != 3)
        {
            Fail($"map {mapLevel} did not produce three actual scaled drops");
            return;
        }

        return;
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
            || _feralDamage.GetValueOrDefault(1) != 8
            || _feralDamage.GetValueOrDefault(4) != 9
            || _spitterDamage.GetValueOrDefault(1) != 4
            || _spitterDamage.GetValueOrDefault(4) != 5
            || _feralDamage.GetValueOrDefault(4) != _feralDamage.GetValueOrDefault(1) + 1
            || _spitterDamage.GetValueOrDefault(4) != _spitterDamage.GetValueOrDefault(1) + 1
            || _bossSlamDamage.GetValueOrDefault(4) != _bossSlamDamage.GetValueOrDefault(1) + 1
            || _bossSpearDamage.GetValueOrDefault(4) != _bossSpearDamage.GetValueOrDefault(1) + 1
            || _dropLevels.GetValueOrDefault(1) != 1
            || _dropLevels.GetValueOrDefault(2) != 2
            || _dropLevels.GetValueOrDefault(4) != 4
            || _actualDropCounts.GetValueOrDefault(1) != 3
            || _actualDropCounts.GetValueOrDefault(2) != 3
            || _actualDropCounts.GetValueOrDefault(4) != 3
            || !_actualCombatLevels.Contains(1)
            || !_actualCombatLevels.Contains(4)
            || !FreshMapOneResourcesAreUnchanged())
        {
            Fail("MapScaling values or actual drop/resource isolation did not match the 1/2/4 contract");
            return;
        }

        _complete = true;
        GD.Print("ENEMY_SCALING_3D_REGRESSION_PASS hp=true damage=true drops=true context_once=true resource_isolated=true");
        GetTree().Quit();
    }

    private bool FreshMapOneResourcesAreUnchanged()
    {
        var feralScene = GD.Load<PackedScene>("res://scenes3d/Feral3D.tscn");
        var spitterScene = GD.Load<PackedScene>("res://scenes3d/Spitter3D.tscn");
        var bossScene = GD.Load<PackedScene>("res://scenes3d/BrimstoneColossus3D.tscn");
        if (feralScene == null || spitterScene == null || bossScene == null)
        {
            return false;
        }

        var feral = feralScene.Instantiate<FeralController3D>();
        var spitter = spitterScene.Instantiate<SpitterController3D>();
        var boss = bossScene.Instantiate<BrimstoneColossusController3D>();
        var unchanged = feral.GetNode<HealthComponent>("HealthComponent").MaxHealth == 40
            && feral.ContactDamage == 8
            && spitter.GetNode<HealthComponent>("HealthComponent").MaxHealth == 24
            && spitter.ProjectileDamage == 4
            && boss.GetNode<HealthComponent>("HealthComponent").MaxHealth == 160;
        feral.Free();
        spitter.Free();
        boss.Free();
        return unchanged;
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
