using System;
using System.Linq;
using Godot;

public partial class QuietCoastPresentation3DRegressionSmoke : Node
{
    private enum Stage { Bind, WaitOcclusion, PauseIntro, VerifyPause, RestoreOccluder, VerifyRestore }

    private Stage _stage;
    private double _elapsed;
    private bool _complete;
    private RunSessionNode _run;
    private TestArena3D _arena;
    private MapIntroBanner3D _intro;
    private CameraOcclusionController3D _occlusion;
    private OccluderVisual3D _arch;
    private ulong _pauseStarted;
    private float _pausedIntroElapsed;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _run = GetNodeOrNull<RunSessionNode>("RunShell3D");
        if (_run == null) Fail("presentation smoke is missing RunShell3D");
    }

    public override void _Process(double delta)
    {
        if (_complete) return;
        _elapsed += delta;
        if (_elapsed > 16.0)
        {
            Fail($"presentation smoke timed out at {_stage}");
            return;
        }

        switch (_stage)
        {
            case Stage.Bind: Bind(); break;
            case Stage.WaitOcclusion: WaitOcclusion(); break;
            case Stage.PauseIntro: PauseIntro(); break;
            case Stage.VerifyPause: VerifyPause(); break;
            case Stage.RestoreOccluder: RestoreOccluder(); break;
            case Stage.VerifyRestore: VerifyRestore(); break;
        }
    }

    private void Bind()
    {
        _arena ??= _run.CurrentMap3D;
        _intro ??= _arena?.GetNodeOrNull<MapIntroBanner3D>("MapIntroBanner3D");
        _occlusion ??= _arena?.GetNodeOrNull<CameraOcclusionController3D>("CameraRig/OcclusionController3D");
        _arch ??= _arena?.GetNodeOrNull<OccluderVisual3D>("MapDressing/ForegroundArch");
        var dressing = _arena?.GetNodeOrNull<Node3D>("MapDressing");
        var environment = _arena?.GetNodeOrNull<WorldEnvironment>("WorldEnvironment")?.Environment;
        var hud = _arena?.GetNodeOrNull<CanvasLayer>("HUD");
        var playerPanel = hud?.GetNodeOrNull<Control>("PlayerPanel");
        var skillPanel = hud?.GetNodeOrNull<Control>("SkillPanel");
        var ailments = playerPanel?.GetNodeOrNull<Control>("Ailments");
        if (_arena == null || _intro == null || _occlusion == null || _arch == null
            || dressing == null || environment == null || playerPanel == null || skillPanel == null || ailments == null)
        {
            return;
        }

        var meshCount = CountRecursive<MeshInstance3D>(dressing);
        var lights = CountRecursive<OmniLight3D>(dressing);
        var beacon = dressing.GetNodeOrNull<MeshInstance3D>("ObjectiveBeacon");
        var beaconMaterial = beacon?.GetActiveMaterial(0) as StandardMaterial3D;
        var nav = _arena.GetNodeOrNull<NavigationRegion3D>("SmallNavigationRegion3D")?.NavigationMesh;
        if (!_arena.SceneFilePath.EndsWith("QuietCoastArena3D.tscn", StringComparison.Ordinal)
            || _arena.AppliedRunPlan?.AtlasMapId != "quiet-coast"
            || meshCount < 11
            || lights < 2
            || environment.Sky == null
            || !environment.FogEnabled
            || beaconMaterial?.EmissionEnabled != true
            || nav == null
            || _intro.MapTitle != "Quiet Coast"
            || !_intro.ObjectiveText.Contains("Esc", StringComparison.Ordinal)
            || playerPanel.Position.Y + playerPanel.Size.Y > skillPanel.Position.Y
            || ailments.Position.Y + ailments.Size.Y > playerPanel.Size.Y)
        {
            Fail($"formal map structure failed scene={_arena.SceneFilePath} meshes={meshCount} lights={lights}");
            return;
        }

        Advance(Stage.WaitOcclusion);
    }

    private void WaitOcclusion()
    {
        if (_occlusion.ProbeCount == 0 || _occlusion.ActiveOccluder != _arch || _arch.CurrentAlpha > 0.75f)
        {
            return;
        }
        Advance(Stage.PauseIntro);
    }

    private void PauseIntro()
    {
        _pausedIntroElapsed = _intro.ElapsedSeconds;
        GetTree().Paused = true;
        _pauseStarted = Time.GetTicksMsec();
        Advance(Stage.VerifyPause);
    }

    private void VerifyPause()
    {
        if (Time.GetTicksMsec() - _pauseStarted < 120) return;
        if (!GetTree().Paused || Math.Abs(_intro.ElapsedSeconds - _pausedIntroElapsed) > 0.001f)
        {
            Fail("map intro advanced while gameplay was paused");
            return;
        }
        GetTree().Paused = false;
        Advance(Stage.RestoreOccluder);
    }

    private void RestoreOccluder()
    {
        var body = _arch.GetNodeOrNull<StaticBody3D>("OcclusionBody");
        if (body == null)
        {
            Fail("formal occluder is missing its dedicated probe body");
            return;
        }
        body.CollisionLayer = 0;
        Advance(Stage.VerifyRestore);
    }

    private void VerifyRestore()
    {
        if (_occlusion.ActiveOccluder != null || _arch.IsOccluded || _arch.CurrentAlpha < 0.9f)
        {
            return;
        }
        _complete = true;
        GD.Print("QUIET_COAST_PRESENTATION_3D_REGRESSION_PASS formal_scene=true environment=true dressing=true objective=true hud_layout=true occlusion=true restore=true intro_pause=true nav_preserved=true");
        GetTree().Quit();
    }

    private static int CountRecursive<T>(Node root) where T : Node =>
        root.GetChildren().Sum(child => (child is T ? 1 : 0) + CountRecursive<T>(child));

    private void Advance(Stage stage)
    {
        _stage = stage;
        _elapsed = 0.0;
    }

    private void Fail(string reason)
    {
        if (_complete) return;
        _complete = true;
        GetTree().Paused = false;
        Engine.TimeScale = 1.0;
        GD.PushError($"QUIET_COAST_PRESENTATION_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}
