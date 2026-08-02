using System;
using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
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

    [Signal]
    public delegate void WaveClearedEventHandler(int waveIndex, string waveId);

    [Signal]
    public delegate void ActiveEnemyCountChangedEventHandler(int activeEnemyCount);

    [Signal]
    public delegate void ActiveEliteCountChangedEventHandler(int activeEliteCount);

    [Export] public EncounterDefinitionResource3D DefinitionResource { get; set; }
    [Export] public bool Enabled { get; set; } = true;
    [Export] public NodePath PlayerPath { get; set; } = new("../Player3D");
    [Export] public NodePath EnemyContainerPath { get; set; } = new("../EnemyContainer");
    [Export] public NodePath SpawnPointsPath { get; set; } = new("../SpawnPoints");

    public EncounterDirectorState3D State { get; private set; } = EncounterDirectorState3D.Disabled;
    public bool IsOperational => Enabled && State != EncounterDirectorState3D.Disabled;
    public PlayerController3D Player => _player;
    public string CurrentEncounterId => DefinitionResource?.EncounterId ?? string.Empty;
    public int CurrentWaveIndex { get; private set; } = -1;
    public int TotalWaveCount => _waves.Count;
    public int ActiveEnemyCount { get; private set; }
    public int SpawnedEnemyCount { get; private set; }
    public int SpawnedBossCount { get; private set; }
    public int EncounterCompletedCount { get; private set; }
    public int CurrentWaveSpawnedCount { get; private set; }
    public int CurrentWaveTargetCount { get; private set; }
    public int BossAddSpawnCount { get; private set; }
    public int ActiveEliteCount => GetActiveEnemies()
        .Count(enemy => enemy is IEliteRuntime3D elite
            && !string.IsNullOrWhiteSpace(elite.EliteModifierId));
    public string LastSpawnPointId { get; private set; } = string.Empty;
    public Node3D ActiveBoss => _spawnedEnemies
        .OfType<BrimstoneColossusController3D>()
        .LastOrDefault(enemy => GodotObject.IsInstanceValid(enemy) && enemy.IsAlive);

    private readonly List<EncounterWaveResource3D> _waves = new();
    private readonly List<EncounterSpawnPoint3D> _spawnPoints = new();
    private readonly List<Node3D> _spawnedEnemies = new();
    private readonly HashSet<Node3D> _countedDead = new();
    private readonly HashSet<int> _clearedWaves = new();
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
        _player = GetNodeOrNull<PlayerController3D>(PlayerPath);
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
        _stateRemaining = DefinitionResource.InitialDelaySeconds;
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
        _clearedWaves.Clear();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (State == EncounterDirectorState3D.Disabled
            || State == EncounterDirectorState3D.Completed)
        {
            return;
        }

        if (_player == null || !GodotObject.IsInstanceValid(_player))
        {
            _player = GetNodeOrNull<PlayerController3D>(PlayerPath);
        }
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

    /// <summary>
    /// Test-only registration for a dynamically configured enemy. Production
    /// encounters use SpawnNextEnemy; this keeps lifecycle assertions on the
    /// same active-count accounting without exposing the spawn state machine.
    /// </summary>
    public bool RegisterExternalEnemyForTest(Node3D enemy)
    {
        if (enemy == null
            || _spawnedEnemies.Contains(enemy)
            || enemy.GetNodeOrNull<HealthComponent>("HealthComponent") == null)
        {
            return false;
        }

        TrackEnemy(enemy);
        EmitSignal(SignalName.ActiveEliteCountChanged, ActiveEliteCount);
        return true;
    }

    /// <summary>
    /// Spawns map-local adds requested by a live boss phase. Adds use the
    /// normal pre-ready configuration and active-enemy accounting, but are
    /// deliberately outside the current wave target so the encounter cannot
    /// advance until every add is dead.
    /// </summary>
    public int TrySpawnBossAdds(string waveId, int count)
    {
        if (count <= 0
            || _enemyContainer == null
            || _player == null
            || !GodotObject.IsInstanceValid(_player))
        {
            return 0;
        }

        var runSession = MapRuntimeScope3D.FindRunSession(this);
        var scene = GD.Load<PackedScene>("res://scenes3d/Feral3D.tscn");
        if (runSession == null || scene == null)
        {
            return 0;
        }

        var mapModifier = runSession.CurrentMapModifier?.Effects ?? new MapModifierStats();
        var dropItemLevel = runSession.CurrentMapPlan.DropItemLevel;
        var encounterId = string.IsNullOrWhiteSpace(CurrentEncounterId)
            ? runSession.CurrentEncounterId
            : CurrentEncounterId;
        var added = 0;
        for (var index = 0; index < count; index++)
        {
            var spawnPoint = ChooseBossAddSpawnPoint();
            if (spawnPoint == null)
            {
                break;
            }

            var enemy = scene.Instantiate<FeralController3D>();
            enemy.Name = $"FeralBossAdd_{CurrentWaveIndex + 1}_{BossAddSpawnCount + added + 1}";
            var context = new EnemySpawnContext3D(
                runSession,
                _player,
                runSession.CurrentMapLevel,
                encounterId,
                string.IsNullOrWhiteSpace(waveId) ? "boss-adds" : waveId,
                _spawnedEnemies.Count + 1,
                1,
                mapModifier,
                dropItemLevel,
                false,
                true);
            try
            {
                context.Validate();
                enemy.ConfigureBeforeReady(context);
                _enemyContainer.AddChild(enemy);
                enemy.GlobalPosition = new Vector3(
                    spawnPoint.GlobalPosition.X,
                    0.0f,
                    spawnPoint.GlobalPosition.Z);
            }
            catch (Exception exception)
            {
                GD.PushError($"Could not configure boss add {enemy.Name}: {exception.Message}");
                enemy.QueueFree();
                continue;
            }

            TrackEnemy(enemy);
            SpawnedEnemyCount++;
            BossAddSpawnCount++;
            added++;
            LastSpawnPointId = spawnPoint.SpawnPointId;
            EmitSignal(SignalName.EnemySpawned, enemy);
            EmitSignal(SignalName.ActiveEliteCountChanged, ActiveEliteCount);
        }

        return added;
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
            if (_clearedWaves.Add(CurrentWaveIndex))
            {
                EmitSignal(
                    SignalName.WaveCleared,
                    CurrentWaveIndex,
                    wave.WaveId);
            }

            if (CurrentWaveIndex + 1 >= _waves.Count)
            {
                BeginWave(_waves.Count);
            }
            else
            {
                State = EncounterDirectorState3D.Intermission;
                _stateRemaining = _waves[CurrentWaveIndex].IntermissionAfterSeconds;
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
        var runSession = MapRuntimeScope3D.FindRunSession(this);
        if (spawnPoint == null || entry.EnemyScene == null || _enemyContainer == null || runSession == null || _player == null)
        {
            return false;
        }

        var enemy = entry.EnemyScene.Instantiate<Node3D>();
        enemy.Name = $"{entry.EnemyScene.ResourceName}_{CurrentWaveIndex + 1}_{CurrentWaveSpawnedCount + 1}";
        var mapLevel = runSession.CurrentMapLevel;
        var modifier = runSession.CurrentMapModifier?.Effects ?? new MapModifierStats();
        var dropItemLevel = runSession.CurrentMapPlan.DropItemLevel;
        var eliteSelection = EliteSelection.Select(
            runSession.Session.RunSeed,
            runSession.CurrentEncounterSeed,
            mapLevel,
            CurrentWaveIndex,
            CurrentWaveSpawnedCount + 1,
            enemy is BrimstoneColossusController3D,
            runSession.CurrentMapModifierId);
        var eliteModifier = string.IsNullOrWhiteSpace(eliteSelection.EliteModifierId)
            ? null
            : EliteModifierLibrary.Find(eliteSelection.EliteModifierId);
        var context = new EnemySpawnContext3D(
            runSession,
            _player,
            mapLevel,
            DefinitionResource.EncounterId,
            wave.WaveId,
            CurrentWaveSpawnedCount + 1,
            entry.NavigationLayers,
            modifier,
            dropItemLevel,
            enemy is BrimstoneColossusController3D)
        {
            EliteModifier = eliteModifier,
            EliteSelectionSeed = eliteSelection.SelectionSeed,
        };
        try
        {
            context.Validate();
            ConfigureNavigationLayers(enemy, entry.NavigationLayers);
            if (enemy is IEnemySpawnConfigurable3D configurable)
            {
                configurable.ConfigureBeforeReady(context);
            }
            else
            {
                GD.PushError($"Encounter enemy {enemy.Name} does not implement IEnemySpawnConfigurable3D.");
                enemy.QueueFree();
                return false;
            }

            _enemyContainer.AddChild(enemy);
            enemy.GlobalPosition = new Vector3(
                spawnPoint.GlobalPosition.X,
                0.0f,
                spawnPoint.GlobalPosition.Z);
        }
        catch (System.Exception exception)
        {
            GD.PushError($"Could not configure encounter enemy {enemy.Name}: {exception.Message}");
            enemy.QueueFree();
            return false;
        }

        TrackEnemy(enemy);
        _entrySpawned++;
        CurrentWaveSpawnedCount++;
        SpawnedEnemyCount++;
        LastSpawnPointId = spawnPoint.SpawnPointId;
        EmitSignal(SignalName.EnemySpawned, enemy);
        EmitSignal(SignalName.ActiveEliteCountChanged, ActiveEliteCount);
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
            if (point.CanSpawn(_player, navigationLayers, _spawnedEnemies))
            {
                _spawnPointCursor = (_spawnPointCursor + offset + 1) % candidates.Length;
                return point;
            }
        }

        return null;
    }

    private EncounterSpawnPoint3D ChooseBossAddSpawnPoint()
    {
        if (_spawnPoints.Count == 0)
        {
            return null;
        }

        var candidates = _spawnPoints
            .Where(point => point.SpawnPointId == "feral" || point.SpawnPointId == "mixed")
            .ToArray();
        if (candidates.Length == 0)
        {
            candidates = _spawnPoints.ToArray();
        }

        for (var offset = 0; offset < candidates.Length; offset++)
        {
            var point = candidates[(_spawnPointCursor + offset) % candidates.Length];
            if (point.CanSpawn(_player, 1, _spawnedEnemies))
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
            EmitSignal(SignalName.ActiveEnemyCountChanged, ActiveEnemyCount);
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
            EmitSignal(SignalName.ActiveEnemyCountChanged, ActiveEnemyCount);
            EmitSignal(SignalName.ActiveEliteCountChanged, ActiveEliteCount);
        }
    }
}
