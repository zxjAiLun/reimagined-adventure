using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public enum EncounterDirectorState3D
{
    Disabled,
    Waiting,
    Spawning,
    Intermission,
    Completed,
}

/// <summary>
/// Map-local encounter state machine. It owns wave timing and spawn
/// lifecycle, while enemy scenes retain all movement and combat behaviour.
/// </summary>
public partial class EncounterDirector3D : Node
{
    [Signal]
    public delegate void EncounterCompletedEventHandler();

    [Signal]
    public delegate void WaveStartedEventHandler(int waveIndex, string waveId);

    [Signal]
    public delegate void EnemySpawnedEventHandler(Node3D enemy);

    [Signal]
    public delegate void BossSpawnedEventHandler(Node3D boss);

    [Export] public EncounterDefinitionResource3D DefinitionResource { get; set; }
    [Export] public bool Enabled { get; set; } = true;
    [Export] public NodePath EnemyContainerPath { get; set; } = new("../EnemyContainer");
    [Export] public NodePath SpawnPointsPath { get; set; } = new("../SpawnPoints");

    public EncounterDirectorState3D State { get; private set; } = EncounterDirectorState3D.Disabled;
    public int CurrentWaveIndex { get; private set; } = -1;
    public int TotalWaveCount => _waves.Count;
    public int ActiveEnemyCount { get; private set; }
    public int SpawnedEnemyCount { get; private set; }
    public int SpawnedBossCount { get; private set; }
    public int EncounterCompletedCount { get; private set; }
    public int CurrentWaveSpawnedCount { get; private set; }
    public int CurrentWaveTargetCount { get; private set; }
    public string LastSpawnPointId { get; private set; } = string.Empty;
    public Node3D ActiveBoss => _spawnedEnemies
        .OfType<BrimstoneColossusController3D>()
        .LastOrDefault(enemy => GodotObject.IsInstanceValid(enemy) && enemy.IsAlive);

    private readonly List<EncounterWaveResource3D> _waves = new();
    private readonly List<EncounterSpawnPoint3D> _spawnPoints = new();
    private readonly List<Node3D> _spawnedEnemies = new();
    private readonly HashSet<Node3D> _countedDead = new();
    private readonly HashSet<HealthComponent> _trackedHealth = new();
    private Node3D _enemyContainer;
    private PlayerController3D _player;
    private float _stateRemaining;
    private float _spawnRemaining;
    private int _entryIndex;
    private int _entrySpawned;
    private int _spawnPointCursor;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        AddToGroup("encounter_directors_3d");
        _player = GetTree().GetFirstNodeInGroup("player_3d") as PlayerController3D;
        _enemyContainer = GetNodeOrNull<Node3D>(EnemyContainerPath);
        CollectSpawnPoints();

        if (!Enabled)
        {
            State = EncounterDirectorState3D.Disabled;
            return;
        }

        var definitionError = string.Empty;
        if (DefinitionResource == null || !DefinitionResource.IsValid(out definitionError))
        {
            GD.PushError($"EncounterDirector3D disabled: {definitionError}");
            State = EncounterDirectorState3D.Disabled;
            return;
        }

        _waves.AddRange(DefinitionResource.Waves);
        State = EncounterDirectorState3D.Waiting;
        _stateRemaining = _waves[0].StartDelaySeconds;
    }

    public override void _ExitTree()
    {
        foreach (var health in _trackedHealth.ToArray())
        {
            if (GodotObject.IsInstanceValid(health))
            {
                health.Died -= OnAnyEnemyDied;
            }
        }

        _trackedHealth.Clear();
        _spawnedEnemies.Clear();
        _countedDead.Clear();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (State == EncounterDirectorState3D.Disabled
            || State == EncounterDirectorState3D.Completed)
        {
            return;
        }

        _player ??= GetTree().GetFirstNodeInGroup("player_3d") as PlayerController3D;
        var frameDelta = Mathf.Max(0.0f, (float)delta);
        switch (State)
        {
            case EncounterDirectorState3D.Waiting:
                _stateRemaining -= frameDelta;
                if (_stateRemaining <= 0.0f)
                {
                    BeginWave(0);
                }

                break;
            case EncounterDirectorState3D.Spawning:
                TickSpawning(frameDelta);
                break;
            case EncounterDirectorState3D.Intermission:
                _stateRemaining -= frameDelta;
                if (_stateRemaining <= 0.0f)
                {
                    BeginWave(CurrentWaveIndex + 1);
                }

                break;
        }
    }

    public IReadOnlyList<Node3D> GetActiveEnemies()
    {
        return _spawnedEnemies
            .Where(enemy => GodotObject.IsInstanceValid(enemy)
                && enemy.GetNodeOrNull<HealthComponent>("HealthComponent")?.IsAlive == true)
            .ToArray();
    }

    public bool IsEncounterComplete() => State == EncounterDirectorState3D.Completed;

    private void CollectSpawnPoints()
    {
        var root = GetNodeOrNull<Node3D>(SpawnPointsPath);
        if (root == null)
        {
            return;
        }

        foreach (var child in root.GetChildren().OfType<EncounterSpawnPoint3D>())
        {
            _spawnPoints.Add(child);
        }
    }

    private void BeginWave(int waveIndex)
    {
        if (waveIndex >= _waves.Count)
        {
            State = EncounterDirectorState3D.Completed;
            EncounterCompletedCount++;
            EmitSignal(SignalName.EncounterCompleted);
            return;
        }

        CurrentWaveIndex = waveIndex;
        CurrentWaveSpawnedCount = 0;
        CurrentWaveTargetCount = _waves[waveIndex].TotalSpawnCount;
        _entryIndex = 0;
        _entrySpawned = 0;
        _spawnRemaining = 0.0f;
        State = EncounterDirectorState3D.Spawning;
        EmitSignal(SignalName.WaveStarted, waveIndex, _waves[waveIndex].WaveId);
    }

    private void TickSpawning(float delta)
    {
        var wave = _waves[CurrentWaveIndex];
        _spawnRemaining -= delta;
        if (CurrentWaveSpawnedCount < CurrentWaveTargetCount
            && ActiveEnemyCount < wave.MaxAlive
            && _spawnRemaining <= 0.0f)
        {
            if (SpawnNextEnemy(wave))
            {
                _spawnRemaining = Mathf.Max(0.0f, wave.SpawnIntervalSeconds);
            }
            else
            {
                _spawnRemaining = 0.2f;
            }
        }

        if (CurrentWaveSpawnedCount >= CurrentWaveTargetCount && ActiveEnemyCount == 0)
        {
            if (CurrentWaveIndex + 1 >= _waves.Count)
            {
                BeginWave(_waves.Count);
            }
            else
            {
                State = EncounterDirectorState3D.Intermission;
                _stateRemaining = _waves[CurrentWaveIndex].IntermissionSeconds;
            }
        }
    }

    private bool SpawnNextEnemy(EncounterWaveResource3D wave)
    {
        while (_entryIndex < wave.Entries.Count
            && _entrySpawned >= wave.Entries[_entryIndex].Count)
        {
            _entryIndex++;
            _entrySpawned = 0;
        }

        if (_entryIndex >= wave.Entries.Count)
        {
            return false;
        }

        var entry = wave.Entries[_entryIndex];
        var spawnPoint = ChooseSpawnPoint(entry.SpawnPointId, entry.NavigationLayers);
        if (spawnPoint == null || entry.EnemyScene == null || _enemyContainer == null)
        {
            return false;
        }

        var enemy = entry.EnemyScene.Instantiate<Node3D>();
        enemy.Name = $"{entry.EnemyScene.ResourceName}_{CurrentWaveIndex + 1}_{CurrentWaveSpawnedCount + 1}";
        _enemyContainer.AddChild(enemy);
        enemy.GlobalPosition = new Vector3(
            spawnPoint.GlobalPosition.X,
            0.0f,
            spawnPoint.GlobalPosition.Z);
        ConfigureNavigationLayers(enemy, entry.NavigationLayers);
        TrackEnemy(enemy);
        _entrySpawned++;
        CurrentWaveSpawnedCount++;
        SpawnedEnemyCount++;
        LastSpawnPointId = spawnPoint.SpawnPointId;
        EmitSignal(SignalName.EnemySpawned, enemy);
        if (enemy is BrimstoneColossusController3D boss)
        {
            SpawnedBossCount++;
            EmitSignal(SignalName.BossSpawned, boss);
        }

        return true;
    }

    private EncounterSpawnPoint3D ChooseSpawnPoint(string spawnPointId, int navigationLayers)
    {
        if (_spawnPoints.Count == 0)
        {
            return null;
        }

        var requested = _spawnPoints
            .Where(point => point.SpawnPointId == spawnPointId)
            .ToArray();
        var candidates = requested.Length > 0 ? requested : _spawnPoints.ToArray();
        for (var offset = 0; offset < candidates.Length; offset++)
        {
            var point = candidates[(_spawnPointCursor + offset) % candidates.Length];
            if (point.CanSpawn(_player, navigationLayers))
            {
                _spawnPointCursor = (_spawnPointCursor + offset + 1) % candidates.Length;
                return point;
            }
        }

        return null;
    }

    private void ConfigureNavigationLayers(Node3D enemy, int navigationLayers)
    {
        var agent = enemy.GetNodeOrNull<NavigationAgent3D>("NavigationAgent3D");
        if (agent != null)
        {
            agent.NavigationLayers = (uint)Mathf.Max(1, navigationLayers);
        }
    }

    private void TrackEnemy(Node3D enemy)
    {
        _spawnedEnemies.Add(enemy);
        var health = enemy.GetNodeOrNull<HealthComponent>("HealthComponent");
        if (health == null)
        {
            GD.PushError($"Encounter enemy {enemy.Name} has no HealthComponent.");
            return;
        }

        _trackedHealth.Add(health);
        health.Died += OnAnyEnemyDied;
        if (health.IsAlive)
        {
            ActiveEnemyCount++;
        }
        else
        {
            _countedDead.Add(enemy);
        }
    }

    private void OnAnyEnemyDied()
    {
        foreach (var enemy in _spawnedEnemies)
        {
            if (!GodotObject.IsInstanceValid(enemy)
                || _countedDead.Contains(enemy))
            {
                continue;
            }

            var health = enemy.GetNodeOrNull<HealthComponent>("HealthComponent");
            if (health == null || health.IsAlive)
            {
                continue;
            }

            _countedDead.Add(enemy);
            ActiveEnemyCount = Mathf.Max(0, ActiveEnemyCount - 1);
        }
    }
}
