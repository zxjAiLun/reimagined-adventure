using System;
using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Exercises all three shipped map modifiers through real pre-ready enemy
/// configuration, damage application, and loot creation. The test also
/// verifies that the outer run resolves a modifier once per map identity.
/// </summary>
public partial class MapModifierRuntime3DRegressionSmoke : Node
{
    private readonly HashSet<string> _knownDropIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _testedModifiers = new(StringComparer.Ordinal);
    private RunSessionNode _run;
    private TestArena3D _arena;
    private PlayerController3D _player;
    private Node3D _enemyContainer;
    private MapModifierCatalogResource3D _catalog;
    private bool _started;
    private bool _complete;
    private double _elapsed;
    private int _stage;
    private int _expectedNewDrops;
    private int _expectedDropLevel;
    private string _expectedModifierId;
    private int _resolvedSignalCount;
    private FeralController3D _activeFeral;
    private SpitterController3D _activeSpitter;
    private BrimstoneColossusController3D _activeBoss;
    private int _attackPhase;
    private float _attackElapsed;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _catalog = GD.Load<MapModifierCatalogResource3D>(
            "res://resources/RunMapModifierCatalog3D.tres");
    }

    public override void _Process(double delta)
    {
        if (_complete)
        {
            return;
        }

        _elapsed += delta;
        BindRuntime();
        if (_run == null || _arena == null || _player == null || _enemyContainer == null)
        {
            if (_elapsed > 10.0)
            {
                Fail("modifier runtime nodes did not become ready");
            }

            return;
        }

        var director = _arena.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        if (director != null)
        {
            director.Enabled = false;
            director.ProcessMode = ProcessModeEnum.Disabled;
        }

        if (_attackPhase != 0)
        {
            TickModifierAttack((float)delta);
            return;
        }

        if (!_started)
        {
            var catalogError = string.Empty;
            if (_catalog == null || !_catalog.IsValid(out catalogError))
            {
                Fail($"modifier catalog invalid: {catalogError}");
                return;
            }

            _run.MapModifierResolved += OnMapModifierResolved;
            if (!RestoreMapLevel(4))
            {
                return;
            }

            var initialId = _run.CurrentMapModifierId;
            var initialResolveCount = _run.MapModifierResolveCount;
            if (_run.CurrentMapModifier == null || string.IsNullOrWhiteSpace(initialId))
            {
                Fail("RunSession did not resolve a current modifier");
                return;
            }

            if (!RestoreMapLevel(4)
                || _run.MapModifierResolveCount != initialResolveCount
                || _run.CurrentMapModifierId != initialId)
            {
                Fail("same-map restore rerolled the modifier");
                return;
            }

            _started = true;
            _stage = 0;
        }

        if (_expectedNewDrops > 0)
        {
            var newDrops = CollectNewDrops();
            if (newDrops < _expectedNewDrops)
            {
                if (_elapsed > 45.0)
                {
                    Fail($"expected {_expectedNewDrops} modifier drops, observed {newDrops}");
                }

                return;
            }

            _expectedNewDrops = 0;
        }

        if (_stage < 3)
        {
            var modifierId = new[] { "quiet-coast", "hardened-front", "volatile-hunt" }[_stage];
            if (!_testedModifiers.Contains(modifierId))
            {
                if (!TestModifier(modifierId))
                {
                    return;
                }

                _testedModifiers.Add(modifierId);
                return;
            }

            _stage++;
            return;
        }

        if (_resolvedSignalCount < 2)
        {
            if (!RestoreMapLevel(1))
            {
                return;
            }

            if (_resolvedSignalCount < 2 || _run.CurrentMapModifier == null)
            {
                if (_elapsed > 45.0)
                {
                    Fail("cross-map modifier resolution signal was not emitted");
                }

                return;
            }
        }

        _complete = true;
        _run.MapModifierResolved -= OnMapModifierResolved;
        GD.Print("MAP_MODIFIER_RUNTIME_3D_REGRESSION_PASS three_modifiers=true real_damage=true real_drops=true cross_map=true");
        GetTree().Quit();
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

    private void OnMapModifierResolved(string modifierId, int mapLevel) => _resolvedSignalCount++;

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
            Fail($"could not set modifier smoke map level to {mapLevel}");
            return false;
        }

        _arena.MapLevel = mapLevel;
        return true;
    }

    private bool TestModifier(string modifierId)
    {
        var definition = _catalog.ResolveDefinition(modifierId);
        var effects = definition.Effects;
        var itemLevel = MapScaling.ItemLevel(4, effects);
        var baseFeralHp = MapScaling.EnemyHp(40, 4, effects);
        var expectedFeralDamage = MapScaling.EnemyDamage(8, 4, effects);
        if (modifierId == "hardened-front" && (baseFeralHp != 88 || itemLevel != 5))
        {
            Fail("Hardened Front did not use the frozen HP/item-level contract");
            return false;
        }

        if (modifierId == "volatile-hunt" && (expectedFeralDamage != 11 || itemLevel != 5))
        {
            Fail("Volatile Hunt did not use the frozen damage/item-level contract");
            return false;
        }

        _expectedModifierId = modifierId;
        var feral = Spawn<FeralController3D>(
            "res://scenes3d/Feral3D.tscn",
            effects,
            itemLevel,
            false,
            new Vector3(-5.0f + _stage * 3.0f, 0.0f, -4.0f));
        var spitter = Spawn<SpitterController3D>(
            "res://scenes3d/Spitter3D.tscn",
            effects,
            itemLevel,
            false,
            new Vector3(-5.0f + _stage * 3.0f, 0.0f, 4.0f));
        var boss = Spawn<BrimstoneColossusController3D>(
            "res://scenes3d/BrimstoneColossus3D.tscn",
            effects,
            itemLevel,
            true,
            new Vector3(5.0f, 0.0f, 0.0f));
        if (feral == null || spitter == null || boss == null)
        {
            return false;
        }

        if (feral.AppliedDropItemLevel != itemLevel
            || spitter.AppliedDropItemLevel != itemLevel
            || boss.AppliedDropItemLevel != itemLevel
            || feral.AppliedPrimaryDamage != expectedFeralDamage
            || feral.AppliedMaxHealth != baseFeralHp)
        {
            Fail($"{modifierId} spawn context did not reach real actors");
            return false;
        }

        _activeFeral = feral;
        _activeSpitter = spitter;
        _activeBoss = boss;
        // Dynamic actors are added with their normal physics processing enabled.
        // Freeze every actor before starting the serialized attack phases so a
        // later phase cannot damage the player while an earlier phase is being
        // observed by the smoke.
        feral.SetPhysicsProcess(false);
        spitter.SetPhysicsProcess(false);
        boss.SetPhysicsProcess(false);
        ClearEnemyProjectiles();
        _player.ApplyRestoredHealth(_player.MaxHealth);
        _player.GlobalPosition = Vector3.Zero;
        feral.GlobalPosition = new Vector3(1.0f, 0.0f, 0.0f);
        spitter.GlobalPosition = new Vector3(0.0f, 0.0f, 5.0f);
        boss.GlobalPosition = new Vector3(2.0f, 0.0f, 0.0f);
        _attackPhase = 1;
        _attackElapsed = 0.0f;
        _expectedDropLevel = itemLevel;
        return true;
    }

    private void TickModifierAttack(float delta)
    {
        _attackElapsed += delta;
        if (_attackElapsed > 5.0f)
        {
            Fail($"{_expectedModifierId} actual attack phase {_attackPhase} timed out");
            return;
        }

        switch (_attackPhase)
        {
            case 1:
                _activeFeral.SetPhysicsProcess(true);
                if (_activeFeral.ImpactCount > 0)
                {
                    var damage = _player.MaxHealth - _player.CurrentHealth;
                    if (damage != _activeFeral.AppliedPrimaryDamage)
                    {
                        Fail($"{_expectedModifierId} Feral actual damage was {damage}, expected {_activeFeral.AppliedPrimaryDamage}");
                        return;
                    }

                    _activeFeral.SetPhysicsProcess(false);
                    _player.ApplyRestoredHealth(_player.MaxHealth);
                    ClearEnemyProjectiles();
                    _activeSpitter.AttackCooldown = 10.0f;
                    _activeSpitter.SetPhysicsProcess(true);
                    _attackPhase = 2;
                    _attackElapsed = 0.0f;
                }

                break;
            case 2:
                if (_activeSpitter.ProjectileShotCount > 0
                    && _player.CurrentHealth < _player.MaxHealth)
                {
                    var damage = _player.MaxHealth - _player.CurrentHealth;
                    if (damage != _activeSpitter.AppliedPrimaryDamage)
                    {
                        Fail($"{_expectedModifierId} Spitter actual damage was {damage}, expected {_activeSpitter.AppliedPrimaryDamage}");
                        return;
                    }

                    _activeSpitter.SetPhysicsProcess(false);
                    _player.ApplyRestoredHealth(_player.MaxHealth);
                    ClearEnemyProjectiles();
                    _activeBoss.SetPhysicsProcess(true);
                    _attackPhase = 3;
                    _attackElapsed = 0.0f;
                }

                break;
            case 3:
                if (_activeBoss.MagmaSlamImpactCount > 0
                    && _player.CurrentHealth < _player.MaxHealth)
                {
                    var damage = _player.MaxHealth - _player.CurrentHealth;
                    if (damage != _activeBoss.AppliedPrimaryDamage)
                    {
                        Fail($"{_expectedModifierId} Boss Slam actual damage was {damage}, expected {_activeBoss.AppliedPrimaryDamage}");
                        return;
                    }

                    _activeBoss.SetPhysicsProcess(false);
                    ClearEnemyProjectiles();
                    Kill(_activeFeral);
                    Kill(_activeSpitter);
                    Kill(_activeBoss);
                    _expectedNewDrops = 3;
                    _attackPhase = 0;
                    _attackElapsed = 0.0f;
                    _activeFeral = null;
                    _activeSpitter = null;
                    _activeBoss = null;
                }

                break;
        }
    }

    private T Spawn<T>(
        string scenePath,
        MapModifierStats modifier,
        int itemLevel,
        bool isBoss,
        Vector3 position)
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
            4,
            "modifier_runtime_smoke",
            _expectedModifierId ?? "modifier",
            _enemyContainer.GetChildCount() + 1,
            isBoss ? 2 : 1,
            modifier,
            itemLevel,
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
            Fail($"modifier context failed for {scenePath}: {exception.Message}");
            enemy.QueueFree();
            return null;
        }
    }

    private static void Kill(ICombatTarget target)
    {
        target.ApplyDamage(new DamageRequest(
            999999,
            DamageType.Physical,
            "modifier_runtime_lethal",
            CombatFaction.Player));
    }

    private void ClearEnemyProjectiles()
    {
        foreach (var node in GetTree().GetNodesInGroup("enemy_projectiles_3d"))
        {
            if (node is Node projectile)
            {
                projectile.QueueFree();
            }
        }
    }

    private int CollectNewDrops()
    {
        var count = 0;
        foreach (var node in GetTree().GetNodesInGroup("item_drops_3d"))
        {
            if (node is not ItemDrop3D drop || drop.Item == null)
            {
                continue;
            }

            if (_knownDropIds.Add(drop.Item.Id))
            {
                count++;
                if (drop.Item.ItemLevel != _expectedDropLevel)
                {
                    Fail($"{_expectedModifierId} drop item level was {drop.Item.ItemLevel}, expected {_expectedDropLevel}");
                    return count;
                }
            }
        }

        return count;
    }

    private void Fail(string reason)
    {
        if (_complete)
        {
            return;
        }

        _complete = true;
        GD.PushError($"MAP_MODIFIER_RUNTIME_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}
