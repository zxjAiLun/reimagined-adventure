using Arpg.Domain;

/// <summary>
/// Immutable inputs selected by the run before a map enters the scene tree.
/// TestArena3D consumes this once to configure its local director pre-ready.
/// </summary>
public sealed record RunMapPlan3D(
    RunSessionNode RunSession,
    int MapLevel,
    MapModifierDefinition Modifier,
    ulong ModifierSeed,
    EncounterDefinitionResource3D Encounter,
    string EncounterId,
    string EncounterDisplayName,
    int EncounterTier,
    ulong EncounterSeed)
{
    public void Validate()
    {
        if (RunSession == null)
        {
            throw new System.ArgumentNullException(nameof(RunSession));
        }

        if (MapLevel < 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(MapLevel));
        }

        System.ArgumentNullException.ThrowIfNull(Modifier);
        Modifier.Validate();
        System.ArgumentNullException.ThrowIfNull(Encounter);
        if (!Encounter.IsValid(out var error))
        {
            throw new System.ArgumentException(error, nameof(Encounter));
        }

        if (string.IsNullOrWhiteSpace(EncounterId)
            || EncounterId != Encounter.EncounterId)
        {
            throw new System.ArgumentException("Encounter plan id does not match its definition.", nameof(EncounterId));
        }

        if (string.IsNullOrWhiteSpace(EncounterDisplayName) || EncounterTier < 1)
        {
            throw new System.ArgumentException("Encounter plan presentation metadata is invalid.");
        }
    }
}
