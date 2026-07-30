using System.Collections.Generic;
using Godot;

/// <summary>
/// Dedicated Stage 3D map. Two saved navigation layers share the same static
/// geometry, while the large-agent layer removes the central narrow route.
/// </summary>
public partial class BossNavigationStressArena3D : Node3D
{
    [Export] public PackedScene FeralScene { get; set; }
    [Export] public PackedScene SpitterScene { get; set; }
    [Export] public PackedScene BossScene { get; set; }
    [Export] public int FeralCount { get; set; } = 1;
    [Export] public int SpitterCount { get; set; }
    [Export] public bool SpawnBoss { get; set; } = true;

    private readonly List<Node3D> _spawnedEnemies = new();
    private BrimstoneColossusController3D _boss;

    public EnemyCrowdCoordinator3D Coordinator =>
        GetNodeOrNull<EnemyCrowdCoordinator3D>("EnemyCrowdCoordinator3D");
    public PlayerController3D Player => GetNodeOrNull<PlayerController3D>("Player3D");
    public BrimstoneColossusController3D Boss => _boss;
    public int SpawnedEnemyCount => _spawnedEnemies.Count;

    public Aabb ObstacleA => new(new Vector3(-2.9f, 0.0f, -7.0f), new Vector3(2.0f, 2.0f, 14.0f));
    public Aabb ObstacleB => new(new Vector3(0.9f, 0.0f, -7.0f), new Vector3(2.0f, 2.0f, 14.0f));
    public Aabb NarrowChoke => new(new Vector3(-0.9f, 0.0f, -7.0f), new Vector3(1.8f, 2.0f, 14.0f));

    public override void _Ready()
    {
        AddToGroup("boss_navigation_stress_arenas_3d");
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
            SpawnEnemy(FeralScene, GetSmallSpawnPosition(startIndex + i));
        }

        for (var i = 0; i < spitterCount; i++)
        {
            SpawnEnemy(SpitterScene, GetSmallSpawnPosition(startIndex + feralCount + i));
        }
    }

    public BrimstoneColossusController3D SpawnBossNow()
    {
        if (_boss != null && GodotObject.IsInstanceValid(_boss))
        {
            return _boss;
        }

        if (BossScene == null)
        {
            return null;
        }

        _boss = BossScene.Instantiate<BrimstoneColossusController3D>();
        AddChild(_boss);
        _boss.AllowDirectChaseWithoutNavigation = false;
        _boss.GlobalPosition = new Vector3(-10.0f, 0.0f, 1.8f);
        _spawnedEnemies.Add(_boss);
        return _boss;
    }

    private void SpawnInitialWave()
    {
        SpawnAdditionalWave(FeralCount, SpitterCount);
        if (SpawnBoss)
        {
            SpawnBossNow();
        }
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
        enemy.AddToGroup("boss_navigation_stress_enemies_3d");
        _spawnedEnemies.Add(enemy);
    }

    private static Vector3 GetSmallSpawnPosition(int index)
    {
        var row = index % 5;
        var column = index / 5;
        return new Vector3(-10.0f + column * 0.7f, 0.0f, -6.0f + row * 2.6f);
    }
}
