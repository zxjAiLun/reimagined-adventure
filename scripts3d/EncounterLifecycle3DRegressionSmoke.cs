using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Covers pause safety and map-local director lifecycle across a real map
/// transition. The second map must create a fresh director and start its own
/// encounter rather than reusing the first map's counters.
/// </summary>
public partial class EncounterLifecycle3DRegressionSmoke : Node
{
    private double _elapsed;
    private bool _complete;
    private bool _pauseTestStarted;
    private bool _pauseTestCompleted;
    private bool _transitionRequested;
    private int _spawnedBeforePause;
    private int _waveBeforePause;
    private EncounterDirector3D _firstDirector;
    private GameFlowController3D _firstFlow;
    private MapRewardNode3D _firstRewards;
    private RunSessionNode _run;

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_complete)
        {
            return;
        }

        if (_transitionRequested)
        {
            VerifySecondMap();
            return;
        }

        BindFirstMap();
        if (_firstDirector == null || _firstFlow == null || _firstRewards == null || _run == null)
        {
            if (_elapsed > 8.0)
            {
                Fail("encounter lifecycle nodes did not become ready");
            }

            return;
        }

        if (!_pauseTestStarted && _firstDirector.State != EncounterDirectorState3D.Completed)
        {
            _pauseTestStarted = true;
            _spawnedBeforePause = _firstDirector.SpawnedEnemyCount;
            _waveBeforePause = _firstDirector.CurrentWaveIndex;
            _firstFlow.RestoreState(GameFlowState.GameOver);
            return;
        }

        if (_pauseTestStarted && !_pauseTestCompleted)
        {
            if (_elapsed < 1.0)
            {
                return;
            }

            if (_firstDirector.SpawnedEnemyCount != _spawnedBeforePause
                || _firstDirector.CurrentWaveIndex != _waveBeforePause)
            {
                Fail("paused encounter continued spawning or advancing waves");
                return;
            }

            _firstFlow.RestoreState(GameFlowState.Playing);
            _pauseTestCompleted = true;
        }

        if (_firstFlow.State == GameFlowState.Playing)
        {
            foreach (var enemy in _firstDirector.GetActiveEnemies())
            {
                ApplyLethalDamage(enemy);
            }
        }

        if (_firstFlow.State == GameFlowState.MapComplete)
        {
            if (!_firstRewards.HasChosen && !_firstRewards.TryChooseReward(0))
            {
                Fail("first-map reward choice failed");
                return;
            }

            if (!_run.LoadNextMap())
            {
                Fail("next-map transition was rejected");
                return;
            }

            _transitionRequested = true;
            return;
        }

        if (_elapsed > 24.0)
        {
            Fail($"lifecycle smoke timed out state={_firstFlow.State} wave={_firstDirector.CurrentWaveIndex}");
        }
    }

    private void BindFirstMap()
    {
        _run ??= GetTree().GetFirstNodeInGroup("run_sessions") as RunSessionNode;
        if (_firstDirector != null && GodotObject.IsInstanceValid(_firstDirector))
        {
            return;
        }

        var arena = GetTree().GetNodesInGroup("arena_3d")
            .OfType<TestArena3D>()
            .FirstOrDefault(map => map.UsesEncounterRuntime);
        _firstDirector = arena?.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        _firstFlow = arena?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        _firstRewards = arena?.GetNodeOrNull<MapRewardNode3D>("MapRewards3D");
    }

    private void VerifySecondMap()
    {
        var secondArena = GetTree().GetNodesInGroup("arena_3d")
            .OfType<TestArena3D>()
            .LastOrDefault(map => map.UsesEncounterRuntime);
        var secondDirector = secondArena?.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        if (secondDirector == null)
        {
            if (_elapsed > 24.0)
            {
                Fail($"second map did not create an encounter director level={_run.CurrentMapLevel}");
            }

            return;
        }

        if (secondDirector == null
            || ReferenceEquals(secondDirector, _firstDirector)
            || secondDirector.State == EncounterDirectorState3D.Disabled
            || _run.CurrentMapLevel != 2)
        {
            Fail($"second map did not create a fresh encounter director level={_run.CurrentMapLevel} director={secondDirector != null}");
            return;
        }

        _complete = true;
        GD.Print("ENCOUNTER_LIFECYCLE_3D_SPIKE_PASS pause_frozen=true map_transition=true fresh_director=true level=2");
        GetTree().Quit();
    }

    private static void ApplyLethalDamage(Node3D enemy)
    {
        var request = new DamageRequest(
            9999,
            DamageType.Physical,
            "encounter_lifecycle_smoke",
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

    private void Fail(string reason)
    {
        _complete = true;
        GD.PushError($"ENCOUNTER_LIFECYCLE_3D_SPIKE_FAIL {reason}");
        GetTree().Quit(1);
    }
}
