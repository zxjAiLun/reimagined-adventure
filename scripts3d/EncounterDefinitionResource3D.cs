using Godot;

/// <summary>
/// Complete data definition for one map encounter.
/// </summary>
[GlobalClass]
public partial class EncounterDefinitionResource3D : Resource
{
    [Export] public string EncounterId { get; set; } = "default_encounter";
    [Export] public Godot.Collections.Array<EncounterWaveResource3D> Waves { get; set; } = new();

    public bool IsValid(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(EncounterId))
        {
            error = "EncounterId is required.";
            return false;
        }

        if (Waves == null || Waves.Count == 0)
        {
            error = "At least one wave is required.";
            return false;
        }

        foreach (var wave in Waves)
        {
            if (wave == null || !wave.IsValid(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Encounter contains an invalid wave.";
                }
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
