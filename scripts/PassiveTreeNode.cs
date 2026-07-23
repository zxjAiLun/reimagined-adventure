using Arpg.Domain;
using Godot;

/// <summary>
/// Minimal Godot boundary for passive allocation. A future Control tree can
/// call TryAllocate without duplicating prerequisite or stat aggregation rules.
/// </summary>
public partial class PassiveTreeNode : Node
{
    [Signal]
    public delegate void PassiveChangedEventHandler();

    [Export] public PassiveTreeResource DefinitionResource { get; set; }

    private PassiveTreeState _state;
    private PlayerController _player;

    public PassiveTreeState State => _state ?? throw new System.InvalidOperationException("PassiveTreeNode is not ready.");

    public override void _Ready()
    {
        var definition = DefinitionResource?.ToDomain() ?? PassiveTreeLibrary.MinimumSlice();
        _state = new PassiveTreeState(definition);
        _player = GetParentOrNull<PlayerController>();
        ApplyStats();
        AddToGroup("passive_trees");
    }

    public bool TryAllocate(int index)
    {
        if (!State.TryAllocate(index))
        {
            return false;
        }

        ApplyStats();
        EmitSignal(SignalName.PassiveChanged);
        return true;
    }

    private void ApplyStats()
    {
        _player?.SetPassiveStats(State.CombinedStats());
    }
}
