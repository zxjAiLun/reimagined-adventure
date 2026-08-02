using Godot;

/// <summary>
/// Small world-space status label. It consumes the component's event and does
/// not inspect health, damage, or SceneTree state.
/// </summary>
public partial class AilmentPresentation3D : Label3D
{
    [Export] public NodePath ComponentPath { get; set; } = new("../AilmentComponent3D");

    private AilmentComponent3D _component;

    public override void _Ready()
    {
        _component = GetNodeOrNull<AilmentComponent3D>(ComponentPath);
        if (_component != null)
        {
            _component.AilmentsChanged += OnAilmentsChanged;
            OnAilmentsChanged(_component.Summary);
        }
    }

    public override void _ExitTree()
    {
        if (_component != null)
        {
            _component.AilmentsChanged -= OnAilmentsChanged;
        }
    }

    private void OnAilmentsChanged(string summary)
    {
        Text = summary;
        Visible = !string.IsNullOrWhiteSpace(summary);
    }
}
