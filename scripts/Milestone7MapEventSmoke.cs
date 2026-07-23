using Godot;

public partial class Milestone7MapEventSmoke : Node2D
{
    private PlayerController _player;
    private MapEventNode _event;

    public override void _Ready()
    {
        _player = GetNode<PlayerController>("Player");
        _event = GetNode<MapEventNode>("MapEvent");
        if (!_event.TryActivate(_player)
            || !_event.IsCompleted
            || GetTree().GetNodesInGroup("item_drops").Count != 2
            || _event.TryActivate(_player))
        {
            GD.PrintErr("MILESTONE7_MAP_EVENT_FAIL");
            GetTree().Quit(1);
            return;
        }

        GD.Print("MILESTONE7_MAP_EVENT_PASS");
        GetTree().Quit(0);
    }
}
