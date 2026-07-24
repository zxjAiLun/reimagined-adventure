using Godot;

/// <summary>
/// Fixed greybox arena for the 3D spike. Geometry is deliberately plain, but
/// it exercises a narrow lane, a height cue, occlusion-sized blocks and a
/// small crowd of independent Feral actors.
/// </summary>
public partial class GreyboxStressArena3D : Node3D
{
    [Export] public PackedScene FeralScene { get; set; }
    [Export] public int EnemyCount { get; set; } = 24;

    public int SpawnedEnemyCount { get; private set; }

    public override void _Ready()
    {
        AddToGroup("greybox_arenas_3d");
        BuildNavigationSurface();
        SpawnPressureWave();
    }

    private void BuildNavigationSurface()
    {
        var region = GetNodeOrNull<NavigationRegion3D>("NavigationRegion3D");
        if (region == null)
        {
            return;
        }

        var mesh = new NavigationMesh
        {
            Vertices = new Vector3[]
            {
                new Vector3(-11, 0.0f, -7),
                new Vector3(11, 0.0f, -7),
                new Vector3(11, 0.0f, 7),
                new Vector3(-11, 0.0f, 7),
            },
        };
        mesh.AddPolygon(new[] { 0, 1, 2, 3 });
        region.NavigationMesh = mesh;
    }

    private void SpawnPressureWave()
    {
        if (FeralScene == null)
        {
            return;
        }

        var count = Mathf.Clamp(EnemyCount, 20, 40);
        for (var index = 0; index < count; index++)
        {
            var angle = Mathf.Tau * index / count;
            var radius = index % 2 == 0 ? 8.5f : 6.8f;
            var feral = FeralScene.Instantiate<FeralController3D>();
            AddChild(feral);
            feral.GlobalPosition = new Vector3(
                Mathf.Cos(angle) * radius,
                0.0f,
                Mathf.Sin(angle) * radius);
            feral.AddToGroup("greybox_stress_enemies");
            SpawnedEnemyCount++;
        }
    }
}
