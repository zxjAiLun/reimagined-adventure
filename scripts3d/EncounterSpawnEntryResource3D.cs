using Godot;

/// <summary>
/// One data-driven enemy batch inside a wave.
/// </summary>
[GlobalClass]
public partial class EncounterSpawnEntryResource3D : Resource
{
    [Export] public PackedScene EnemyScene { get; set; }
    [Export] public int Count { get; set; } = 1;
    [Export] public string SpawnPointId { get; set; } = "default";
    [Export] public int NavigationLayers { get; set; } = 1;

    public bool IsValid(out string error)
    {
        if (EnemyScene == null)
        {
            error = "EnemyScene is required.";
            return false;
        }

        if (Count < 1)
        {
            error = "Count must be positive.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SpawnPointId))
        {
            error = "SpawnPointId is required.";
            return false;
        }

        if (NavigationLayers < 1)
        {
            error = "NavigationLayers must be positive.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
