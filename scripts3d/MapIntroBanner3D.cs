using Godot;

public partial class MapIntroBanner3D : CanvasLayer
{
    [Export] public float HoldSeconds { get; set; } = 3.2f;
    [Export] public float FadeSeconds { get; set; } = 0.8f;

    public string MapTitle { get; private set; } = "Quiet Coast";
    public string ObjectiveText { get; private set; } = "Clear the encounter";
    public float ElapsedSeconds { get; private set; }
    public bool IsComplete { get; private set; }

    private Control _root;
    private Label _title;
    private Label _objective;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        _root = GetNodeOrNull<Control>("BannerRoot");
        _title = GetNodeOrNull<Label>("BannerRoot/Panel/Content/MapTitle");
        _objective = GetNodeOrNull<Label>("BannerRoot/Panel/Content/Objective");

        var run = MapRuntimeScope3D.FindRunSession(this);
        MapTitle = run?.CurrentAtlasMap?.Name ?? "Quiet Coast";
        var encounter = run?.CurrentEncounterDisplayName ?? "Quiet Coast Skirmish";
        ObjectiveText = $"Clear {encounter} · Esc: controls and settings";
        if (_title != null) _title.Text = MapTitle.ToUpperInvariant();
        if (_objective != null) _objective.Text = ObjectiveText;
    }

    public override void _Process(double delta)
    {
        if (IsComplete || _root == null)
        {
            return;
        }

        ElapsedSeconds += (float)delta;
        var fadeStart = Mathf.Max(0.0f, HoldSeconds);
        var alpha = ElapsedSeconds <= fadeStart
            ? 1.0f
            : 1.0f - (ElapsedSeconds - fadeStart) / Mathf.Max(0.01f, FadeSeconds);
        _root.Modulate = new Color(1.0f, 1.0f, 1.0f, Mathf.Clamp(alpha, 0.0f, 1.0f));
        if (ElapsedSeconds >= fadeStart + Mathf.Max(0.01f, FadeSeconds))
        {
            IsComplete = true;
            _root.Visible = false;
        }
    }
}
