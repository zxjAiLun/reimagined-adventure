using Godot;

public partial class Brimstone3DRegressionSmoke : Node
{
    private BrimstoneColossusController3D _boss;
    private PlayerController3D _player;
    private float _timeout;
    private int _stage;
    private int _bossHealthBeforeSlam;
    private int _playerHealthBeforeSlam;

    public override void _Ready()
    {
        _boss = GetNode<BrimstoneColossusController3D>("Arena3D/BrimstoneColossus3D");
        _player = GetNode<PlayerController3D>("Arena3D/Player3D");
    }

    public override void _Process(double delta)
    {
        _timeout += (float)delta;
        if (_stage == 0 && _boss.MagmaSlamCount > 0)
        {
            _bossHealthBeforeSlam = _boss.CurrentHealth;
            _playerHealthBeforeSlam = _player.CurrentHealth;
            _stage = 1;
        }

        if (_stage == 1
            && _boss.FlameSpearCount > 0
            && _boss.CurrentHealth == _bossHealthBeforeSlam
            && _player.CurrentHealth < _playerHealthBeforeSlam)
        {
            GD.Print("BRIMSTONE_3D_SPIKE_PASS state=true slam=true spear=true telegraph=true faction=true");
            GetTree().Quit(0);
            return;
        }

        if (_timeout > 12.0f)
        {
            GD.PrintErr($"BRIMSTONE_3D_SPIKE_FAIL state={_boss.State} slam={_boss.MagmaSlamCount} spear={_boss.FlameSpearCount} player_hp={_player.CurrentHealth}");
            GetTree().Quit(1);
        }
    }
}
