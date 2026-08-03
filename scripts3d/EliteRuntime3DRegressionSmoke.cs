using System;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Exercises elite modifiers through the same pre-ready configuration and
/// death/lifecycle paths used by the encounter director.
/// </summary>
public partial class EliteRuntime3DRegressionSmoke : Node
{
    private double _elapsed;
    private int _stage;
    private bool _complete;
    private RunSessionNode _run;
    private TestArena3D _arena;
    private PlayerController3D _player;
    private Node3D _enemyContainer;
    private EncounterDirector3D _director;
    private GameFlowController3D _flow;
    private FeralController3D _bulwark;
    private FeralController3D _frenzied;
    private FeralController3D _frostbound;
    private SpitterController3D _volcanic;
    private VolcanicDeathEffect3D _volcanicEffect;
    private int _healthBeforeVolcanic;
    private int _experienceBeforeFrostbound;
    private int _dropCountBefore;
    private bool _pausedEffectCancelled;

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_complete)
        {
            return;
        }

        BindRuntime();
        if (_run == null || _arena == null || _player == null || _enemyContainer == null
            || _director == null || _flow == null)
        {
            if (_elapsed > 10.0)
            {
                Fail("elite smoke runtime did not become ready");
            }

            return;
        }

        switch (_stage)
        {
            case 0:
                PrepareEnemies();
                break;
            case 1:
                WaitForFrostboundAttack();
                break;
            case 2:
                KillVolcanic();
                break;
            case 3:
                VerifyVolcanicExplosion();
                break;
            case 4:
                FinishFrostboundAndTestPause();
                break;
            case 5:
                VerifyCompletion();
                break;
        }

        if (_elapsed > 22.0 && !_complete)
        {
            Fail($"elite runtime smoke timed out at stage {_stage}");
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
        _director ??= _arena?.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        _flow ??= _arena?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        if (_director != null)
        {
            _director.Enabled = false;
            _director.ProcessMode = ProcessModeEnum.Disabled;
        }
    }

    private void PrepareEnemies()
    {
        if (_run.CurrentMapLevel != 2)
        {
            var session = _run.Session;
            if (!_run.TryRestore(
                    session.RunSeed,
                    session.ItemSequence,
                    2,
                    session.LootRandom.State,
                    session.CraftingRandom.State,
                    session.EventRandom.State))
            {
                Fail("could not prepare map level 2 for elite runtime");
            }

            return;
        }

        _bulwark = Spawn<FeralController3D>(
            "res://scenes3d/Feral3D.tscn",
            EliteModifierLibrary.Find("bulwark")!,
            1,
            new Vector3(-5.0f, 0.0f, -5.0f));
        _frenzied = Spawn<FeralController3D>(
            "res://scenes3d/Feral3D.tscn",
            EliteModifierLibrary.Find("frenzied")!,
            2,
            new Vector3(5.0f, 0.0f, -5.0f));
        _frostbound = Spawn<FeralController3D>(
            "res://scenes3d/Feral3D.tscn",
            EliteModifierLibrary.Find("frostbound")!,
            3,
            new Vector3(1.0f, 0.0f, 0.0f));
        _volcanic = Spawn<SpitterController3D>(
            "res://scenes3d/Spitter3D.tscn",
            EliteModifierLibrary.Find("volcanic")!,
            4,
            new Vector3(4.0f, 0.0f, 0.0f));
        if (_bulwark == null || _frenzied == null || _frostbound == null || _volcanic == null)
        {
            return;
        }

        _bulwark.SetPhysicsProcess(false);
        _frenzied.SetPhysicsProcess(false);
        _volcanic.SetPhysicsProcess(false);
        if (_director.ActiveEnemyCount != 4 || _director.ActiveEliteCount != 4)
        {
            Fail($"active elite accounting was incorrect enemies={_director.ActiveEnemyCount} elites={_director.ActiveEliteCount}");
            return;
        }

        if (_bulwark.AppliedMaxHealth != 75
            || _bulwark.AppliedPrimaryDamage != 8
            || Math.Abs(_bulwark.MoveSpeed - 2.16f) > 0.001f
            || _bulwark.GetNode<HealthComponent>("HealthComponent").DefensiveStats.Armor != 8
            || _frenzied.AppliedPrimaryDamage != 10
            || Math.Abs(_frenzied.MoveSpeed - 2.88f) > 0.001f
            || Math.Abs(_frenzied.AttackWindupSeconds - 0.28f) > 0.001f
            || _frostbound.AppliedMaxHealth != 58
            || _volcanic.AppliedPrimaryDamage != 5
            || _bulwark.EliteAppliedCount != 1
            || _frenzied.EliteAppliedCount != 1
            || _frostbound.EliteAppliedCount != 1
            || _volcanic.EliteAppliedCount != 1
            || string.IsNullOrWhiteSpace(_bulwark.EliteModifierId)
            || !_bulwark.GetNode<Label3D>("HealthLabel").Text.Contains("[Bulwark]", StringComparison.Ordinal))
        {
            Fail("elite pre-ready health, damage, timing, armor, or diagnostics were incorrect");
            return;
        }

        _player.GlobalPosition = new Vector3(0.0f, 0.0f, 0.0f);
        _frostbound.GlobalPosition = new Vector3(0.7f, 0.0f, 0.0f);
        _frostbound.SetPhysicsProcess(true);
        _stage = 1;
        _elapsed = 0.0;
    }

    private void WaitForFrostboundAttack()
    {
        if (_frostbound.ImpactCount <= 0)
        {
            if (_elapsed > 5.0)
            {
                Fail("Frostbound did not reach an attack impact");
            }

            return;
        }

        var chill = _player.Ailments.Collection.Get(AilmentKind.Chilled);
        if (chill == null
            || chill.RemainingSeconds > 1.55
            || chill.RemainingSeconds < 1.25)
        {
            Fail($"Frostbound did not apply the frozen 1.5 second Chill remaining={chill?.RemainingSeconds}");
            return;
        }

        _frostbound.SetPhysicsProcess(false);
        _player.ApplyRestoredHealth(_player.MaxHealth);
        _stage = 2;
        _elapsed = 0.0;
    }

    private void KillVolcanic()
    {
        _volcanic.GlobalPosition = new Vector3(0.8f, 0.0f, 0.0f);
        _player.GlobalPosition = Vector3.Zero;
        _healthBeforeVolcanic = _player.CurrentHealth;
        _volcanic.ApplyDamage(new DamageRequest(
            99999,
            DamageType.Physical,
            "elite_runtime_kill",
            CombatFaction.Player));
        _volcanicEffect = _volcanic.ActiveVolcanicDeathEffect;
        if (!_volcanic.IsAlive
            && _volcanicEffect != null
            && _volcanicEffect.ActiveTelegraph != null
            && _director.ActiveEnemyCount == 3
            && _director.ActiveEliteCount == 3)
        {
            _stage = 3;
            _elapsed = 0.0;
            return;
        }

        if (_elapsed > 3.0)
        {
            Fail("Volcanic death did not create a telegraph or update active counts");
        }
    }

    private void VerifyVolcanicExplosion()
    {
        if (_elapsed < 0.95)
        {
            return;
        }

        if (!GodotObject.IsInstanceValid(_volcanicEffect)
            || _volcanicEffect.ExplosionCount != 1
            || _volcanicEffect.ExplosionRadius != 1.8f
            || _player.CurrentHealth >= _healthBeforeVolcanic)
        {
            Fail("Volcanic telegraph did not produce exactly one real fire explosion");
            return;
        }

        _dropCountBefore = CountDrops();
        _experienceBeforeFrostbound = _run.TotalExperience;
        _stage = 4;
        _elapsed = 0.0;
    }

    private void FinishFrostboundAndTestPause()
    {
        if (_elapsed < 0.1)
        {
            _frostbound.ApplyDamage(new DamageRequest(
                99999,
                DamageType.Physical,
                "elite_runtime_frostbound_kill",
                CombatFaction.Player));
            _frostbound.ApplyDamage(new DamageRequest(
                99999,
                DamageType.Physical,
                "elite_runtime_frostbound_duplicate",
                CombatFaction.Player));
            return;
        }

        var pausedVolcanic = Spawn<FeralController3D>(
            "res://scenes3d/Feral3D.tscn",
            EliteModifierLibrary.Find("volcanic")!,
            5,
            new Vector3(0.5f, 0.0f, 0.0f));
        if (pausedVolcanic == null)
        {
            return;
        }

        pausedVolcanic.SetPhysicsProcess(false);
        pausedVolcanic.ApplyDamage(new DamageRequest(
            99999,
            DamageType.Physical,
            "elite_runtime_pause_kill",
            CombatFaction.Player));
        var effect = pausedVolcanic.ActiveVolcanicDeathEffect;
        _flow.RestoreState(GameFlowState.GameOver);
        _pausedEffectCancelled = effect == null
            || !GodotObject.IsInstanceValid(effect)
            || effect.IsCancelled;
        _flow.RestoreState(GameFlowState.Playing);

        if (_run.TotalExperience != _experienceBeforeFrostbound + 16
            || CountDrops() < _dropCountBefore + 1
            || _director.ActiveEliteCount != 2
            || !_pausedEffectCancelled)
        {
            Fail($"elite death reward or pause contract failed xp={_run.TotalExperience} drops={CountDrops()} elites={_director.ActiveEliteCount} paused={_pausedEffectCancelled}");
            return;
        }

        _stage = 5;
        _elapsed = 0.0;
    }

    private void VerifyCompletion()
    {
        if (_elapsed < 0.15)
        {
            return;
        }

        var freshScene = GD.Load<PackedScene>("res://scenes3d/Feral3D.tscn");
        var fresh = freshScene?.Instantiate<FeralController3D>();
        var isolated = fresh != null
            && fresh.GetNode<HealthComponent>("HealthComponent").MaxHealth == 40
            && fresh.EliteAppliedCount == 0;
        fresh?.Free();
        if (!isolated)
        {
            Fail("elite configuration mutated the shared Feral scene resource");
            return;
        }

        _complete = true;
        GD.Print("ELITE_RUNTIME_3D_REGRESSION_PASS pre_ready=true stats=true frostbound=true volcanic=true active_counts=true xp_once=true loot=true pause_cancel=true resource_isolated=true");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GetTree().Quit();
    }

    private T Spawn<T>(
        string scenePath,
        EliteModifierDefinition elite,
        int ordinal,
        Vector3 position)
        where T : Node3D, IEnemySpawnConfigurable3D
    {
        var scene = GD.Load<PackedScene>(scenePath);
        if (scene == null)
        {
            Fail($"could not load elite scene {scenePath}");
            return null;
        }

        var enemy = scene.Instantiate<T>();
        var selectionSeed = EliteSelection.Select(
            _run.Session.RunSeed,
            _run.CurrentEncounterSeed,
            2,
            0,
            ordinal,
            false,
            "quiet-coast").SelectionSeed;
        var context = new EnemySpawnContext3D(
            _run,
            _player,
            2,
            "elite_runtime_smoke",
            "elite_wave",
            ordinal,
            1,
            new MapModifierStats(),
            2,
            false)
        {
            EliteModifier = elite,
            EliteSelectionSeed = selectionSeed,
        };
        try
        {
            context.Validate();
            enemy.ConfigureBeforeReady(context);
            switch (enemy)
            {
                case FeralController3D feral:
                    feral.ForceGuaranteedDropForTest = true;
                    break;
                case SpitterController3D spitter:
                    spitter.ForceGuaranteedDropForTest = true;
                    break;
            }

            _enemyContainer.AddChild(enemy);
            enemy.GlobalPosition = position;
            if (!_director.RegisterExternalEnemyForTest(enemy))
            {
                Fail($"could not register elite {elite.Id} with encounter accounting");
                return null;
            }

            return enemy;
        }
        catch (Exception exception)
        {
            Fail($"elite pre-ready configuration failed: {exception.Message}");
            enemy.QueueFree();
            return null;
        }
    }

    private int CountDrops() => GetTree().GetNodesInGroup("item_drops_3d")
        .OfType<ItemDrop3D>()
        .Count(drop => drop.Item != null);

    private void Fail(string reason)
    {
        if (_complete)
        {
            return;
        }

        _complete = true;
        GD.PushError($"ELITE_RUNTIME_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}
