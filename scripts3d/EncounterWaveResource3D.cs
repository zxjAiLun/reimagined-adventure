using Godot;

/// <summary>
/// Data for one encounter wave. The director owns timing and lifecycle;
/// this resource only describes what should be spawned.
/// </summary>
[GlobalClass]
public partial class EncounterWaveResource3D : Resource
{
    [Export] public string WaveId { get; set; } = "wave";
    [Export] public float StartDelaySeconds { get; set; } = 0.4f;
    [Export] public float SpawnIntervalSeconds { get; set; } = 0.35f;
    [Export] public float IntermissionSeconds { get; set; } = 0.5f;
    [Export] public int MaxAlive { get; set; } = 6;
    [Export] public Godot.Collections.Array<EncounterSpawnEntryResource3D> Entries { get; set; } = new();

    public int TotalSpawnCount
    {
        get
        {
            var total = 0;
            foreach (var entry in Entries ?? new Godot.Collections.Array<EncounterSpawnEntryResource3D>())
            {
                if (entry != null)
                {
                    total += Mathf.Max(0, entry.Count);
                }
            }

            return total;
        }
    }

    public bool IsValid(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(WaveId))
        {
            error = "WaveId is required.";
            return false;
        }

        if (StartDelaySeconds < 0.0f || SpawnIntervalSeconds < 0.0f || IntermissionSeconds < 0.0f)
        {
            error = "Wave timing values cannot be negative.";
            return false;
        }

        if (MaxAlive < 1)
        {
            error = "MaxAlive must be positive.";
            return false;
        }

        if (Entries == null || Entries.Count == 0)
        {
            error = "At least one spawn entry is required.";
            return false;
        }

        foreach (var entry in Entries)
        {
            if (entry == null || !entry.IsValid(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Wave contains an invalid spawn entry.";
                }
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
