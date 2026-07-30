using System.Collections.Generic;
using Godot;

/// <summary>
/// Dedicated Stage 3C pressure arena. It owns only dynamic enemy creation and
/// the map-specific geometry; crowd steering remains in the shared adapter.
/// </summary>
public partial class CrowdNavigationStressArena3D : Node3D
{
    [Export] public PackedScene FeralScene { get; set; }
    [Export] public PackedScene SpitterScene { get; set; }
    [Export] public int FeralCount { get; set; } = 16;
    [Export] public int SpitterCount { get; set; } = 8;

    private readonly List<Node3D> _spawnedEnemies = new();

    public EnemyCrowdCoordinator3D Coordinator =>
        GetNodeOrNull<EnemyCrowdCoordinator3D>("EnemyCrowdCoordinator3D");
    public PlayerController3D Player => GetNodeOrNull<PlayerController3D>("Player3D");
    public int SpawnedEnemyCount => _spawnedEnemies.Count;
    public Aabb ObstacleA => new(new Vector3(-1.2f, 0.0f, -4.2f), new Vector3(2.4f, 2.0f, 8.4f));
    public Aabb ObstacleB => new(new Vector3(1.2f, 0.0f, -6.7f), new Vector3(4.6f, 2.0f, 2.4f));
    public Vector3 ChokeCheckpoint => new(2.3f, 0.0f, 0.0f);

    public override void _Ready()
    {
        AddToGroup("crowd_navigation_stress_arenas_3d");
        var targeting = Player?.GetNodeOrNull<MouseGroundTargeting3D>("MouseGroundTargeting3D");
        var camera = GetNodeOrNull<Camera3D>("CameraRig/Camera3D");
        if (targeting != null && camera != null)
        {
            targeting.Camera = camera;
        }

        CallDeferred(nameof(SpawnInitialWave));
    }

    public void SpawnAdditionalWave(int feralCount, int spitterCount)
    {
        var startIndex = _spawnedEnemies.Count;
        for (var i = 0; i < feralCount; i++)
        {
            SpawnEnemy(FeralScene, GetSpawnPosition(startIndex + i, true));
        }

        for (var i = 0; i < spitterCount; i++)
        {
            SpawnEnemy(SpitterScene, GetSpawnPosition(startIndex + feralCount + i, false));
        }
    }

    private void SpawnInitialWave()
    {
        SpawnAdditionalWave(FeralCount, SpitterCount);
    }

    private void SpawnEnemy(PackedScene scene, Vector3 position)
    {
        if (scene == null)
        {
            return;
        }

        var enemy = scene.Instantiate<Node3D>();
        AddChild(enemy);
        enemy.GlobalPosition = position;
        enemy.AddToGroup("crowd_stress_enemies_3d");
        _spawnedEnemies.Add(enemy);
    }

    private static Vector3 GetSpawnPosition(int index, bool feral)
    {
        // Six deliberately tight spawn slots produce an observable initial
        // overlap. The remaining slots form two staggered rows on the left
        // side of the central wall.
        if (index < 6)
        {
            return new Vector3(-11.5f + (index % 3) * 0.12f, 0.0f, -0.35f + (index / 3) * 0.12f);
        }

        var row = (index - 6) % 4;
        var column = (index - 6) / 4;
        var z = -8.0f + row * 2.35f;
        var x = -11.5f + column * 0.8f + (feral ? 0.0f : 0.3f);
        return new Vector3(x, 0.0f, z);
    }
}
