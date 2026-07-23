using System;
using System.Collections.Generic;
using Arpg.Domain;
using Godot;

/// <summary>
/// Godot adapter for atlas progression. It exposes only map identity and
/// progression events; unlock rules remain in AtlasState.
/// </summary>
public partial class AtlasNode : Node
{
    [Signal]
    public delegate void MapCompletedEventHandler(string mapId);

    [Export] public AtlasDefinitionResource DefinitionResource { get; set; }

    private AtlasState _state;

    public AtlasState State => _state ?? throw new InvalidOperationException("AtlasNode is not ready.");
    public IReadOnlyList<AtlasMapDefinition> AvailableMaps => State.AvailableMaps;

    public override void _Ready()
    {
        var definition = DefinitionResource?.ToDomain() ?? AtlasLibrary.MinimumSlice();
        _state = new AtlasState(definition);
        AddToGroup("atlas");
    }

    public bool TryCompleteMap(string mapId)
    {
        if (!State.TryComplete(mapId))
        {
            return false;
        }

        EmitSignal(SignalName.MapCompleted, mapId);
        return true;
    }
}
