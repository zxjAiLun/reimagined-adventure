using System;
using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Verifies the data-driven three-wave encounter without calling private
/// director methods or manufacturing enemy nodes outside the normal spawn
/// path.
/// </summary>
public partial class EncounterDirector3DRegressionSmoke : Node
{
    private double _elapsed;
    private bool _complete;
    private EncounterDirector3D _director;
    private GameFlowController3D _flow;
    private readonly Dictionary<int, int> _feralByWave = new();
    private readonly Dictionary<int, int> _spitterByWave = new();
    private int _bossSpawnCount;

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

        BindDirector();
        if (_director == null || _flow == null)
        {
            if (_elapsed > 8.0)
            {
                Fail("encounter director or flow did not become ready");
            }

            return;
        }

        if (_flow.State == GameFlowState.Playing)
        {
            foreach (var enemy in _director.GetActiveEnemies())
            {
                ApplyLethalDamage(enemy);
            }
        }

        if (_director.IsEncounterComplete())
        {
            VerifyCompletedEncounter();
            return;
        }

        if (_elapsed > 20.0)
        {
            Fail($"encounter did not complete state={_director.State} wave={_director.CurrentWaveIndex} active={_director.ActiveEnemyCount}");
        }
    }

    private void BindDirector()
    {
        if (_director != null && GodotObject.IsInstanceValid(_director))
        {
            return;
        }

        var arena = GetTree().GetNodesInGroup("arena_3d")
            .OfType<TestArena3D>()
            .LastOrDefault();
        _director = arena?.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        _flow = arena?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        if (_director == null)
        {
            return;
        }

        _director.WaveStarted += OnWaveStarted;
        _director.EnemySpawned += OnEnemySpawned;
    }

    private void OnWaveStarted(int waveIndex, string waveId)
    {
        if (waveIndex < 0)
        {
            return;
        }

        _feralByWave.TryAdd(waveIndex, 0);
        _spitterByWave.TryAdd(waveIndex, 0);
    }

    private void OnEnemySpawned(Node3D enemy)
    {
        var wave = _director.CurrentWaveIndex;
        switch (enemy)
        {
            case FeralController3D:
                _feralByWave[wave] = _feralByWave.GetValueOrDefault(wave) + 1;
                break;
            case SpitterController3D:
                _spitterByWave[wave] = _spitterByWave.GetValueOrDefault(wave) + 1;
                break;
            case BrimstoneColossusController3D:
                _bossSpawnCount++;
                break;
        }
    }

    private void VerifyCompletedEncounter()
    {
        var arena = _director.GetParent<TestArena3D>();
        if (_director.EncounterCompletedCount != 1
            || _director.TotalWaveCount != 3
            || _director.SpawnedEnemyCount != 10
            || _director.SpawnedBossCount != 1
            || _bossSpawnCount != 1)
        {
            Fail($"unexpected encounter totals waves={_director.TotalWaveCount} spawned={_director.SpawnedEnemyCount} bosses={_director.SpawnedBossCount} completed={_director.EncounterCompletedCount}");
            return;
        }

        if (_feralByWave.GetValueOrDefault(0) != 4
            || _feralByWave.GetValueOrDefault(1) != 3
            || _spitterByWave.GetValueOrDefault(1) != 2
            || _feralByWave.GetValueOrDefault(2) != 0
            || _spitterByWave.GetValueOrDefault(2) != 0)
        {
            Fail("wave composition did not match the encounter definition");
            return;
        }

        if (arena?.GetNodeOrNull<FeralController3D>("Feral3D") != null
            || arena?.GetNodeOrNull<SpitterController3D>("Spitter3D") != null
            || arena?.GetNodeOrNull<BrimstoneColossusController3D>("BrimstoneColossus3D") != null)
        {
            Fail("production TestArena3D still contains static enemy nodes");
            return;
        }

        if (_flow.State != GameFlowState.MapComplete || !GetTree().Paused)
        {
            Fail($"encounter completion did not enter paused MapComplete state={_flow.State} paused={GetTree().Paused}");
            return;
        }

        _complete = true;
        GD.Print("ENCOUNTER_DIRECTOR_3D_SPIKE_PASS waves=true composition=true completion_once=true map_complete=true paused=true");
        GetTree().Quit();
    }

    private static void ApplyLethalDamage(Node3D enemy)
    {
        var request = new DamageRequest(
            9999,
            DamageType.Physical,
            "encounter_director_smoke",
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
        GD.PushError($"ENCOUNTER_DIRECTOR_3D_SPIKE_FAIL {reason}");
        GetTree().Quit(1);
    }
}
