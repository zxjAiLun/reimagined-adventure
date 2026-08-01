using Godot;

public partial class TestArena3D : Node3D
{
    public int MapLevel { get; set; } = 1;
    public bool UsesEncounterRuntime => GetParent() is RunSessionNode;
    public RunMapPlan3D AppliedRunPlan { get; private set; }
    public int RunPlanAppliedCount { get; private set; }

    public void ConfigureBeforeReady(RunMapPlan3D plan)
    {
        if (plan == null)
        {
            throw new System.ArgumentNullException(nameof(plan));
        }

        if (RunPlanAppliedCount != 0)
        {
            throw new System.InvalidOperationException("TestArena3D run plan was already applied.");
        }

        plan.Validate();
        MapLevel = plan.MapLevel;
        var director = GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D")
            ?? throw new System.InvalidOperationException("TestArena3D is missing EncounterDirector3D.");
        director.DefinitionResource = plan.Encounter;
        AppliedRunPlan = plan;
        RunPlanAppliedCount = 1;
    }

    public override void _Ready()
    {
        AddToGroup("arena_3d");

        // Wire the scene-local camera explicitly. Keeping this adapter
        // explicit makes the spike deterministic even when the editor has
        // not populated group metadata for an instanced scene yet.
        var player = GetNode<PlayerController3D>("Player3D");
        var targeting = player.GetNode<MouseGroundTargeting3D>("MouseGroundTargeting3D");
        targeting.Camera = GetNode<Camera3D>("CameraRig/Camera3D");

        if (!UsesEncounterRuntime)
        {
            DisableEncounterRuntimeForLegacySmoke();
            CreateLegacyCombatFixtures();
        }
    }

    private void DisableEncounterRuntimeForLegacySmoke()
    {
        var director = GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        if (director == null)
        {
            return;
        }

        director.Enabled = false;
        director.ProcessMode = ProcessModeEnum.Disabled;
    }

    private void CreateLegacyCombatFixtures()
    {
        CreateLegacyEnemy<FeralController3D>(
            "Feral3D",
            "res://scenes3d/Feral3D.tscn",
            new Vector3(4.0f, 0.0f, 0.0f));
        CreateLegacyEnemy<SpitterController3D>(
            "Spitter3D",
            "res://scenes3d/Spitter3D.tscn",
            new Vector3(-4.0f, 0.0f, -3.0f));
        CreateLegacyEnemy<BrimstoneColossusController3D>(
            "BrimstoneColossus3D",
            "res://scenes3d/BrimstoneColossus3D.tscn",
            new Vector3(7.0f, 0.0f, 4.0f));
    }

    private void CreateLegacyEnemy<T>(string nodeName, string scenePath, Vector3 position)
        where T : Node3D
    {
        if (GetNodeOrNull<T>(nodeName) != null)
        {
            return;
        }

        var scene = GD.Load<PackedScene>(scenePath);
        if (scene == null)
        {
            GD.PushError($"Could not load legacy smoke fixture: {scenePath}");
            return;
        }

        var enemy = scene.Instantiate<T>();
        enemy.Name = nodeName;
        enemy.Position = position;
        AddChild(enemy);
    }
}
