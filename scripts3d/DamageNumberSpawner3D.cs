using System.Collections.Generic;
using Arpg.Domain;
using Godot;

/// <summary>
/// Subscribes once to the map's damage sources and creates one number per
/// positive DamageResult. There is deliberately no SceneTree health scan.
/// </summary>
public partial class DamageNumberSpawner3D : Node3D
{
    [Signal]
    public delegate void DamageNumberSpawnedEventHandler(int damage);

    [Export] public PackedScene DamageNumberScene { get; set; }

    public int SpawnedCount { get; private set; }
    public string LastSpawnedText { get; private set; } = string.Empty;
    public DamageNumber3D LastSpawnedNumber { get; private set; }

    private readonly HashSet<DamageFeedbackSource3D> _sources = new();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        AddToGroup("damage_number_spawners_3d");
        CallDeferred(nameof(BindMapSources));
    }

    public override void _ExitTree()
    {
        foreach (var source in _sources)
        {
            if (GodotObject.IsInstanceValid(source))
            {
                source.DamageTaken -= OnDamageTaken;
            }
        }

        _sources.Clear();
    }

    public void Register(DamageFeedbackSource3D source)
    {
        if (source == null || !GodotObject.IsInstanceValid(source) || !source.IsInsideTree())
        {
            return;
        }

        if (_sources.Add(source))
        {
            source.DamageTaken += OnDamageTaken;
        }
    }

    public void Unregister(DamageFeedbackSource3D source)
    {
        if (source == null || !_sources.Remove(source))
        {
            return;
        }

        if (GodotObject.IsInstanceValid(source))
        {
            source.DamageTaken -= OnDamageTaken;
        }
    }

    private void BindMapSources()
    {
        if (!IsInsideTree())
        {
            return;
        }

        foreach (var node in GetTree().GetNodesInGroup("damage_feedback_sources_3d"))
        {
            if (node is DamageFeedbackSource3D source)
            {
                Register(source);
            }
        }
    }

    private void OnDamageTaken(DamageResult result, Vector3 worldPosition)
    {
        if (result.DamageApplied <= 0 || !IsInsideTree())
        {
            return;
        }

        var number = DamageNumberScene?.Instantiate<DamageNumber3D>()
            ?? new DamageNumber3D();
        AddChild(number);
        number.Configure(result.DamageApplied, worldPosition);
        LastSpawnedNumber = number;
        LastSpawnedText = number.Text;
        SpawnedCount++;
        EmitSignal(SignalName.DamageNumberSpawned, result.DamageApplied);
    }
}
