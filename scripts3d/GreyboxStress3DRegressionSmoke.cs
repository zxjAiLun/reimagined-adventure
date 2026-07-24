using Godot;

public partial class GreyboxStress3DRegressionSmoke : Node
{
    private double _elapsed;
    private bool _complete;

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_complete)
        {
            return;
        }

        var arena = GetTree().GetFirstNodeInGroup("greybox_arenas_3d") as GreyboxStressArena3D;
        if (arena == null)
        {
            if (_elapsed > 5.0)
            {
                Fail("greybox arena did not become ready");
            }

            return;
        }

        var navigation = arena.GetNodeOrNull<NavigationRegion3D>("NavigationRegion3D");
        var obstacle = arena.GetNodeOrNull<StaticBody3D>("NarrowPathObstacle");
        var slope = arena.GetNodeOrNull<MeshInstance3D>("SlopeCue");
        var building = arena.GetNodeOrNull<MeshInstance3D>("OccluderBuilding");
        var enemyCount = GetTree().GetNodesInGroup("greybox_stress_enemies").Count;
        if (arena.SpawnedEnemyCount >= 20
            && enemyCount >= 20
            && navigation?.NavigationMesh != null
            && obstacle != null
            && slope != null
            && building != null)
        {
            _complete = true;
            GD.Print($"GREYBOX_3D_SPIKE_PASS enemies={enemyCount} navigation_surface=true narrow_path=true height_cue=true occluder=true");
            GetTree().Quit();
            return;
        }

        if (_elapsed > 8.0)
        {
            Fail($"enemies={enemyCount} spawned={arena.SpawnedEnemyCount} navigation={navigation?.NavigationMesh != null}");
        }
    }

    private void Fail(string reason)
    {
        _complete = true;
        GD.PushError($"GREYBOX_3D_SPIKE_FAIL {reason}");
        GetTree().Quit(1);
    }
}
