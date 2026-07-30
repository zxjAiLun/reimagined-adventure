using System.Collections.Generic;
using Godot;

/// <summary>
/// Map-owned spawn marker. The encounter director chooses among these markers
/// and keeps encounter data independent from scene coordinates.
/// </summary>
public partial class EncounterSpawnPoint3D : Marker3D
{
    [Export] public string SpawnPointId { get; set; } = "spawn";
    [Export] public int NavigationLayers { get; set; } = 1;
    [Export] public float MinimumPlayerDistance { get; set; } = 3.0f;
    [Export] public float OccupancyRadius { get; set; } = 1.0f;

    public bool CanSpawn(
        PlayerController3D player,
        int requiredNavigationLayers,
        IEnumerable<Node3D> activeEnemies)
    {
        if (requiredNavigationLayers > 0
            && (NavigationLayers & requiredNavigationLayers) == 0)
        {
            return false;
        }

        if (player == null || !GodotObject.IsInstanceValid(player))
        {
            return false;
        }

        var offset = GlobalPosition - player.GlobalPosition;
        offset.Y = 0.0f;
        if (offset.Length() < Mathf.Max(0.0f, MinimumPlayerDistance))
        {
            return false;
        }

        var occupancyRadius = Mathf.Max(0.0f, OccupancyRadius);
        if (occupancyRadius <= 0.0f || activeEnemies == null)
        {
            return true;
        }

        foreach (var enemy in activeEnemies)
        {
            if (enemy == null || !GodotObject.IsInstanceValid(enemy))
            {
                continue;
            }

            var health = enemy.GetNodeOrNull<HealthComponent>("HealthComponent");
            if (health == null || !health.IsAlive)
            {
                continue;
            }

            var enemyOffset = GlobalPosition - enemy.GlobalPosition;
            enemyOffset.Y = 0.0f;
            if (enemyOffset.Length() < occupancyRadius)
            {
                return false;
            }
        }

        return true;
    }
}
