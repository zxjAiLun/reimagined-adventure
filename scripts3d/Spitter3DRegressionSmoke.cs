using Godot;

public partial class Spitter3DRegressionSmoke : Node
{
    private SpitterController3D _spitter;
    private float _timeout;

    public override void _Ready()
    {
        _spitter = GetNode<SpitterController3D>("Arena3D/Spitter3D");
    }

    public override void _Process(double delta)
    {
        _timeout += (float)delta;
        if (_spitter.ProjectileShotCount > 0
            && _spitter.TelegraphCount > 0
            && _spitter.State != SpitterState3D.Dead)
        {
            GD.Print("SPITTER_3D_SPIKE_PASS range=true telegraph=true projectile=true");
            GetTree().Quit(0);
            return;
        }

        if (_timeout > 8.0f)
        {
            GD.PrintErr($"SPITTER_3D_SPIKE_FAIL state={_spitter.State} telegraph={_spitter.TelegraphCount} projectile={_spitter.ProjectileShotCount}");
            GetTree().Quit(1);
        }
    }
}
