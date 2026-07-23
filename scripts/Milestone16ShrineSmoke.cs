using Godot;

public partial class Milestone16ShrineSmoke : Node2D
{
    private PlayerController _player;
    private MapEventNode _shrine;
    private double _damageBefore;

    public override void _Ready()
    {
        _player = GetNode<PlayerController>("Player");
        _shrine = GetNode<MapEventNode>("Shrine");
        _damageBefore = _player.EffectiveStats.DamageMultiplier;

        if (!_shrine.TryActivate(_player)
            || !_shrine.IsCompleted
            || !_shrine.HasActiveShrineBuff
            || _player.EffectiveStats.DamageMultiplier <= _damageBefore
            || _shrine.TryActivate(_player))
        {
            GD.PrintErr("MILESTONE16_SHRINE_FAIL");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"MILESTONE16_SHRINE_PASS damage={_damageBefore:0.00}->{_player.EffectiveStats.DamageMultiplier:0.00}");
        GetTree().Quit(0);
    }
}
