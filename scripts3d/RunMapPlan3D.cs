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
    ulong EncounterSeed,
    string AtlasMapId,
    string AtlasMapDisplayName,
    int AtlasTier,
    int DropItemLevel)
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

        if (RunSession.CurrentMapLevel != MapLevel)
        {
            throw new System.ArgumentException(
                "Run plan map level does not match its RunSession.",
                nameof(MapLevel));
        }

        System.ArgumentNullException.ThrowIfNull(Modifier);
        Modifier.Validate();
        if (!ReferenceEquals(Modifier, RunSession.CurrentMapModifier))
        {
            throw new System.ArgumentException(
                "Run plan modifier does not match its RunSession.",
                nameof(Modifier));
        }
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

        if (ModifierSeed == EncounterSeed)
        {
            throw new System.ArgumentException(
                "Modifier and encounter seeds must be isolated.");
        }

        if (!string.IsNullOrWhiteSpace(AtlasMapId)
            && (string.IsNullOrWhiteSpace(AtlasMapDisplayName)
                || AtlasTier < 1))
        {
            throw new System.ArgumentException("Atlas route metadata is invalid.");
        }

        if (DropItemLevel < 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(DropItemLevel));
        }

        if (!string.IsNullOrWhiteSpace(AtlasMapId)
            && RunSession.CurrentAtlasMapId != AtlasMapId)
        {
            throw new System.ArgumentException(
                "Run plan atlas map does not match its RunSession.",
                nameof(AtlasMapId));
        }

        if (!string.IsNullOrWhiteSpace(AtlasMapId)
            && RunSession.CurrentAtlasMap?.MapModifierId != Modifier.Id)
        {
            throw new System.ArgumentException(
                "Run plan modifier does not match its Atlas map.",
                nameof(Modifier));
        }
    }
}
