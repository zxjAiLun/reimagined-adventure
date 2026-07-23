public interface IPlayerInteractable
{
    int InteractionPriority { get; }

    bool CanInteract(PlayerController player);

    bool TryInteract(PlayerController player);
}
