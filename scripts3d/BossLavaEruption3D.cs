using Arpg.Domain;
using Godot;

/// <summary>
/// Map-local, deterministic delayed boss hazard. It owns a pausable
/// telegraph clock and delegates the actual hit to the normal area effect.
/// </summary>
public partial class BossLavaEruption3D : Node3D
{
    public bool IsActive { get; private set; }
    public bool IsCancelled { get; private set; }
    public bool IsComplete { get; private set; }
    public int ExplosionCount { get; private set; }
    public ulong DeterministicSeed { get; private set; }
    public Vector3 ExplosionCenter { get; private set; }
    public float ExplosionRadius { get; private set; }
    public AreaTelegraph3D ActiveTelegraph => _telegraph;

    private Vector3 _center;
    private int _damage;
    private DamageType _damageType;
    private float _remaining;
    private float _postImpactRemaining;
    private float _telegraphDuration;
    private AreaTelegraph3D _telegraph;
    private GameFlowController3D _flow;
    private bool _configured;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
    }

    public void Configure(
        Vector3 center,
        float radius,
        float telegraphDuration,
        int damage,
        DamageType damageType,
        ulong deterministicSeed)
    {
        if (radius <= 0.0f || telegraphDuration <= 0.0f || damage < 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(radius));
        }

        _center = new Vector3(center.X, 0.04f, center.Z);
        _damage = damage;
        _damageType = damageType;
        _telegraphDuration = telegraphDuration;
        _remaining = telegraphDuration;
        ExplosionCenter = _center;
        ExplosionRadius = radius;
        DeterministicSeed = deterministicSeed;
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

        if (_flow != null && _flow.State != GameFlowState.Playing)
        {
            Cancel();
            return;
        }

        _remaining -= Mathf.Max(0.0f, (float)delta);
        _telegraph?.SetProgress(
            1.0f - _remaining / Mathf.Max(0.01f, _telegraphDuration));
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
        _telegraph.Activate(ExplosionRadius, _center, _telegraphDuration);
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

        var scene = GD.Load<PackedScene>("res://scenes3d/SkillAreaEffect3D.tscn");
        if (scene == null || GetParent() is not Node3D map)
        {
            return;
        }

        var effect = scene.Instantiate<SkillAreaEffect3D>();
        map.AddChild(effect);
        effect.ConfigureHazard(
            _center,
            ExplosionRadius,
            0.0,
            0.18,
            new DamageRequest(
                _damage,
                _damageType,
                "lava_eruption_3d",
                CombatFaction.Enemy));
        effect.SetVisualVisible(false);
        effect.ApplyImpactNow();
        effect.QueueFree();
    }
}
