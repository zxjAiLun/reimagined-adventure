using Arpg.Domain;

/// <summary>
/// Immutable, map-owned inputs applied to an enemy before it enters the scene
/// tree. The context is deliberately created by EncounterDirector3D so actor
/// _Ready methods cannot observe unscaled health or damage.
/// </summary>
public sealed record EnemySpawnContext3D(
    RunSessionNode RunSession,
    PlayerController3D Player,
    int MapLevel,
    string EncounterId,
    string WaveId,
    int SpawnOrdinal,
    int NavigationLayers,
    MapModifierStats MapModifier,
    int DropItemLevel,
    bool IsBoss,
    bool IsBossAdd = false)
{
    public EliteModifierDefinition EliteModifier { get; init; }
    public ulong EliteSelectionSeed { get; init; }

    public void Validate()
    {
        if (RunSession == null)
        {
            throw new System.ArgumentNullException(nameof(RunSession));
        }

        if (Player == null)
        {
            throw new System.ArgumentNullException(nameof(Player));
        }

        if (MapLevel < 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(MapLevel));
        }

        if (string.IsNullOrWhiteSpace(EncounterId))
        {
            throw new System.ArgumentException("EncounterId is required.", nameof(EncounterId));
        }

        if (string.IsNullOrWhiteSpace(WaveId))
        {
            throw new System.ArgumentException("WaveId is required.", nameof(WaveId));
        }

        if (SpawnOrdinal < 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(SpawnOrdinal));
        }

        if (NavigationLayers < 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(NavigationLayers));
        }

        if (DropItemLevel < 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(DropItemLevel));
        }

        if (RunSession.CurrentMapLevel != MapLevel)
        {
            throw new System.ArgumentException("Spawn context map level does not match its RunSession.", nameof(MapLevel));
        }

        var modifier = MapModifier ?? throw new System.ArgumentNullException(nameof(MapModifier));
        modifier.Validate();
        EliteModifier?.Validate();
        if (IsBossAdd && IsBoss)
        {
            throw new System.ArgumentException("Boss adds cannot use a boss spawn context.", nameof(IsBossAdd));
        }

        if (EliteModifier != null)
        {
            if (IsBoss)
            {
                throw new System.ArgumentException("Bosses cannot receive an elite modifier.", nameof(EliteModifier));
            }

            if (!EliteModifier.IsEligible(MapLevel))
            {
                throw new System.ArgumentException(
                    "Elite modifier is not eligible for the spawn map level.",
                    nameof(EliteModifier));
            }

            if (EliteSelectionSeed == 0)
            {
                throw new System.ArgumentException(
                    "Elite spawns require a non-zero selection seed.",
                    nameof(EliteSelectionSeed));
            }
        }
    }
}

public interface IEnemySpawnConfigurable3D
{
    void ConfigureBeforeReady(EnemySpawnContext3D context);
}
