using Godot;

/// <summary>
/// Owns the single F-key interaction route. Candidates are sorted by explicit
/// priority, so a map event wins over an item drop at the same position.
/// </summary>
public partial class InteractionController : Node
{
    public override void _Ready()
    {
        SetProcessUnhandledInput(true);
        AddToGroup("interaction_controllers");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("pickup_item", true))
        {
            return;
        }

        var flow = GetTree().GetFirstNodeInGroup("game_flows") as GameFlowController;
        if (flow != null && flow.State != GameFlowState.Playing)
        {
            return;
        }

        var player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
        if (player == null || !player.IsAlive)
        {
            return;
        }

        if (TryInteractNearest())
        {
            GetViewport().SetInputAsHandled();
        }
    }

    public bool TryInteractNearest()
    {
        var player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
        if (player == null || !player.IsAlive)
        {
            return false;
        }

        IPlayerInteractable selected = null;
        var selectedPriority = int.MinValue;
        var selectedDistance = float.MaxValue;

        foreach (var node in GetTree().GetNodesInGroup("interactables"))
        {
            if (node is not IPlayerInteractable candidate || !candidate.CanInteract(player))
            {
                continue;
            }

            var distance = node is Node2D node2D
                ? node2D.GlobalPosition.DistanceTo(player.GlobalPosition)
                : float.MaxValue;
            if (candidate.InteractionPriority > selectedPriority
                || candidate.InteractionPriority == selectedPriority && distance < selectedDistance)
            {
                selected = candidate;
                selectedPriority = candidate.InteractionPriority;
                selectedDistance = distance;
            }
        }

        return selected != null && selected.TryInteract(player);
    }
}
