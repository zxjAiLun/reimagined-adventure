using Arpg.Domain;
using Godot;

/// <summary>
/// Map-local Volcanic death hazard. It owns only the telegraph clock and
/// creates the normal area-effect damage at impact; it never creates another
/// elite or consumes any run random stream.
/// </summary>
public partial class VolcanicDeathEffect3D : Node3D
{
    public bool IsActive { get; private set; }
    public bool IsCancelled { get; private set; }
    public bool IsComplete { get; private set; }
    public int ExplosionCount { get; private set; }
    public Vector3 ExplosionCenter { get; private set; }
    public float ExplosionRadius { get; private set; }
    public AreaTelegraph3D ActiveTelegraph => _telegraph;

    private EliteDeathEffectDefinition _definition;
    private Vector3 _center;
    private int _damage;
    private float _remaining;
    private float _postImpactRemaining;
    private AreaTelegraph3D _telegraph;
    private GameFlowController3D _flow;
    private bool _configured;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
    }

    public void Configure(
        Vector3 center,
        EliteDeathEffectDefinition definition,
        int damage)
    {
        definition.Validate();
        _definition = definition;
        _center = new Vector3(center.X, 0.04f, center.Z);
        _damage = Mathf.Max(1, damage);
        _remaining = (float)definition.TelegraphDurationSeconds;
        ExplosionCenter = _center;
        ExplosionRadius = (float)definition.ExplosionRadius;
        _configured = true;
        IsActive = true;
        _flow = GetParent()?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        if (_flow != null)
        {
            _flow.StateChanged += OnFlowStateChanged;
        }
        CreateTelegraph();
    }

    public override void _ExitTree()
    {
        if (_flow != null)
        {
            _flow.StateChanged -= OnFlowStateChanged;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsComplete)
        {
            _postImpactRemaining -= Mathf.Max(0.0f, (float)delta);
            if (_postImpactRemaining <= 0.0f)
            {
                QueueFree();
            }

            return;
        }

        if (!_configured || !IsActive || IsCancelled)
        {
            return;
        }

        var flow = GetParent()?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        if (flow != null && flow.State != GameFlowState.Playing)
        {
            Cancel();
            return;
        }

        _remaining -= Mathf.Max(0.0f, (float)delta);
        _telegraph?.SetProgress(
            1.0f - _remaining / Mathf.Max(0.01f, (float)_definition.TelegraphDurationSeconds));
        if (_remaining <= 0.0f)
        {
            ApplyImpact();
        }
    }

    public void Cancel()
    {
        if (!IsActive && !IsCancelled)
        {
            return;
        }

        _telegraph?.Cancel();
        _telegraph = null;
        IsActive = false;
        IsCancelled = true;
        QueueFree();
    }

    private void OnFlowStateChanged(int state)
    {
        if (state != (int)GameFlowState.Playing)
        {
            Cancel();
        }
    }

    private void CreateTelegraph()
    {
        var scene = GD.Load<PackedScene>("res://scenes3d/AreaTelegraph3D.tscn");
        if (scene == null)
        {
            return;
        }

        _telegraph = scene.Instantiate<AreaTelegraph3D>();
        AddChild(_telegraph);
        _telegraph.Activate(
            (float)_definition.ExplosionRadius,
            _center,
            (float)_definition.TelegraphDurationSeconds);
    }

    private void ApplyImpact()
    {
        if (!IsActive || IsCancelled || IsComplete)
        {
            return;
        }

        IsActive = false;
        IsComplete = true;
        _telegraph?.Complete();
        _telegraph = null;
        ExplosionCount++;
        _postImpactRemaining = 0.35f;

        var map = GetParent() as Node3D;
        var scene = GD.Load<PackedScene>("res://scenes3d/SkillAreaEffect3D.tscn");
        if (map == null || scene == null)
        {
            return;
        }

        var effect = scene.Instantiate<SkillAreaEffect3D>();
        map.AddChild(effect);
        effect.ConfigureHazard(
            _center,
            _definition.ExplosionRadius,
            0.0,
            0.18,
            new DamageRequest(
                _damage,
                _definition.DamageType,
                "volcanic_death_3d",
                CombatFaction.Enemy));
        effect.SetVisualVisible(false);
        effect.ApplyImpactNow();
        effect.QueueFree();
    }
}
