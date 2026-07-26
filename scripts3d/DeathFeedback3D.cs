using Godot;

/// <summary>
/// Short death presentation owned by the actor. Actors disable physics and
/// collision before calling Play; this component never controls gameplay
/// lifetime or MapComplete.
/// </summary>
public partial class DeathFeedback3D : Node
{
    [Export] public NodePath MeshPath { get; set; } = new("../Mesh");
    [Export] public float DurationSeconds { get; set; } = 0.35f;
    [Export] public float EndScaleMultiplier { get; set; } = 0.72f;

    public int PlayCount { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsComplete { get; private set; }
    public float Progress { get; private set; }
    public Vector3 RestingScale => _restingScale;

    private Node3D _owner;
    private MeshInstance3D _mesh;
    private Vector3 _restingScale = Vector3.One;
    private float _elapsedSeconds;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        _owner = GetParent() as Node3D;
        _mesh = GetNodeOrNull<MeshInstance3D>(MeshPath);
        _restingScale = _owner?.Scale ?? Vector3.One;
    }

    public void Play()
    {
        if (_owner == null || IsActive || IsComplete)
        {
            return;
        }

        PlayCount++;
        IsActive = true;
        IsComplete = false;
        Progress = 0.0f;
        _elapsedSeconds = 0.0f;
        _owner.Scale = _restingScale;
        if (_mesh != null)
        {
            _mesh.Visible = true;
        }
    }

    public void ResetPresentation()
    {
        IsActive = false;
        IsComplete = false;
        Progress = 0.0f;
        _elapsedSeconds = 0.0f;
        if (_owner != null)
        {
            _owner.Scale = _restingScale;
        }

        if (_mesh != null)
        {
            _mesh.Visible = true;
        }
    }

    public override void _Process(double delta)
    {
        if (!IsActive || _owner == null)
        {
            return;
        }

        _elapsedSeconds += Mathf.Max(0.0f, (float)delta);
        Progress = Mathf.Clamp(
            _elapsedSeconds / Mathf.Max(0.01f, DurationSeconds),
            0.0f,
            1.0f);
        var scaleMultiplier = Mathf.Lerp(1.0f, EndScaleMultiplier, Progress);
        _owner.Scale = _restingScale * scaleMultiplier;
        if (Progress >= 1.0f)
        {
            IsActive = false;
            IsComplete = true;
            if (_mesh != null)
            {
                _mesh.Visible = false;
            }
        }
    }
}
