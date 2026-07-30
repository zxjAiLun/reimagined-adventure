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

    public bool CanSpawn(PlayerController3D player, int requiredNavigationLayers)
    {
        if (requiredNavigationLayers > 0
            && (NavigationLayers & requiredNavigationLayers) == 0)
        {
            return false;
        }

        if (player == null || !GodotObject.IsInstanceValid(player))
        {
            return true;
        }

        var offset = GlobalPosition - player.GlobalPosition;
        offset.Y = 0.0f;
        return offset.Length() >= Mathf.Max(0.0f, MinimumPlayerDistance);
    }
}
