using Godot;
using System;
using System.Linq;
using Arpg.Domain;

public partial class RunLoop3DRegressionSmoke : Node
{
    private double _elapsed;
    private bool _complete;
    private bool _expectedStateCaptured;
    private Stats _expectedRewardStats;
    private string[] _expectedInventoryIds = Array.Empty<string>();
    private string _expectedEquippedWeaponId;
    private int _expectedCurrentHealth;
    private int _expectedItemSequence;
    private ulong _expectedRunSeed;
    private ulong _expectedLootRandomState;
    private ulong _expectedCraftingRandomState;
    private ulong _expectedEventRandomState;

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
        var flow = arena?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        var rewards = arena?.GetNodeOrNull<MapRewardNode3D>("MapRewards3D");
        var director = arena?.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        var save = arena?.GetNodeOrNull<SaveBoundaryNode3D>("SaveBoundary3D");
        var player = arena?.GetNodeOrNull<PlayerController3D>("Player3D");
        if (run == null || flow == null || rewards == null || director == null || save == null || player == null)
        {
            if (_elapsed > 8.0)
            {
                Fail($"3D run-loop nodes did not become ready run={run != null} flow={flow != null} rewards={rewards != null} director={director != null} save={save != null} player={player != null}");
            }

            return;
        }

        if (flow.State == GameFlowState.MapComplete && !rewards.HasChosen)
        {
            if (!rewards.TryChooseReward(0))
            {
                Fail("map reward choice was rejected");
                return;
            }

            var build = arena.GetNodeOrNull<BuildIntermissionController3D>("BuildIntermission3D");
            if (build == null || !build.TryCompleteBuildForTest())
            {
                Fail("build intermission did not complete before next-map transition");
                return;
            }

            CaptureExpectedTransitionState(run, player);
            return;
        }

        if (flow.State == GameFlowState.MapComplete && rewards.HasChosen)
        {
            if (!_expectedStateCaptured)
            {
                Fail("next-map transition began without a captured reward state");
                return;
            }

            if (!run.LoadNextMap())
            {
                Fail("3D next-map transition was rejected");
                return;
            }

            return;
        }

        if (run.CurrentMapLevel >= 2 && flow.State == GameFlowState.Playing)
        {
            if (!_expectedStateCaptured)
            {
                Fail("map level advanced without a captured transition state");
                return;
            }

            if (!player.RewardStats.EquivalentTo(_expectedRewardStats))
            {
                if (_elapsed > 12.0)
                {
                    Fail($"selected reward was not restored on the next map damage_multiplier={player.RewardStats.DamageMultiplier} expected={_expectedRewardStats.DamageMultiplier}");
                }

                return;
            }

            if (!TryValidateRestoredState(run, player, out var restoreError))
            {
                Fail(restoreError);
                return;
            }

            var savePass = save.TrySaveCurrentRun(out var saveError);
            var loadError = string.Empty;
            var loadPass = savePass
                && save.TryLoadAndApplyLastRun(out _, out loadError);
            if (!savePass || !loadPass)
            {
                Fail($"3D save/restore failed save={saveError} load={loadError}");
                return;
            }

            _complete = true;
            GD.Print(
                $"RUN_LOOP_3D_SPIKE_PASS map_complete=true reward=true next_map=true save_restore=true level={run.CurrentMapLevel} state_continuity=true rng_continuity=true");
            // Flush Godot-managed arrays before the process exits; the managed wrapper
            // finalizer must run while the native Godot runtime is still alive.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GetTree().Quit();
            return;
        }

        if (flow.State == GameFlowState.Playing)
        {
            foreach (var enemy in director.GetActiveEnemies())
            {
                ApplyLethalDamage(enemy);
            }

            return;
        }

        if (_elapsed > 12.0)
        {
            Fail($"map={run.CurrentMapLevel} state={flow.State}");
        }
    }

    private void CaptureExpectedTransitionState(RunSessionNode run, PlayerController3D player)
    {
        _expectedStateCaptured = true;
        _expectedRewardStats = player.RewardStats;
        _expectedInventoryIds = player.Items
            .Select(item => item.Id)
            .ToArray();
        _expectedEquippedWeaponId = player.EquippedWeapon?.Id;
        _expectedCurrentHealth = player.CurrentHealth;
        _expectedItemSequence = run.Session.ItemSequence;
        _expectedRunSeed = run.Session.RunSeed;
        _expectedLootRandomState = run.Session.LootRandom.State;
        _expectedCraftingRandomState = run.Session.CraftingRandom.State;
        _expectedEventRandomState = run.Session.EventRandom.State;
    }

    private bool TryValidateRestoredState(
        RunSessionNode run,
        PlayerController3D player,
        out string error)
    {
        if (run.CurrentMapLevel != 2)
        {
            error = $"next map level was not restored: {run.CurrentMapLevel}";
            return false;
        }

        var inventoryIds = player.Items.Select(item => item.Id).ToArray();
        if (!inventoryIds.SequenceEqual(_expectedInventoryIds))
        {
            error = $"inventory IDs were not restored: {string.Join(',', inventoryIds)} expected={string.Join(',', _expectedInventoryIds)}";
            return false;
        }

        if (player.EquippedWeapon?.Id != _expectedEquippedWeaponId)
        {
            error = $"equipped weapon was not restored: {player.EquippedWeapon?.Id ?? "none"} expected={_expectedEquippedWeaponId ?? "none"}";
            return false;
        }

        if (player.CurrentHealth != _expectedCurrentHealth)
        {
            error = $"current health was not restored: {player.CurrentHealth} expected={_expectedCurrentHealth}";
            return false;
        }

        if (run.Session.ItemSequence != _expectedItemSequence
            || run.Session.RunSeed != _expectedRunSeed)
        {
            error = $"run identity was not restored: seed={run.Session.RunSeed} item_sequence={run.Session.ItemSequence}";
            return false;
        }

        if (run.Session.LootRandom.State != _expectedLootRandomState
            || run.Session.CraftingRandom.State != _expectedCraftingRandomState
            || run.Session.EventRandom.State != _expectedEventRandomState)
        {
            error = "one or more deterministic RNG states were not restored";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void Fail(string reason)
    {
        _complete = true;
        GD.PushError($"RUN_LOOP_3D_SPIKE_FAIL {reason}");
        GetTree().Quit(1);
    }

    private static void ApplyLethalDamage(Node3D enemy)
    {
        var request = new DamageRequest(
            9999,
            DamageType.Physical,
            "run_loop_smoke",
            CombatFaction.Player);
        switch (enemy)
        {
            case FeralController3D feral:
                feral.ApplyDamage(request);
                break;
            case SpitterController3D spitter:
                spitter.ApplyDamage(request);
                break;
            case BrimstoneColossusController3D boss:
                boss.ApplyDamage(request);
                break;
        }
    }
}
