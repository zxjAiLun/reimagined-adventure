using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Map-complete route presentation. AtlasState and RunSessionNode own the
/// progression rules; this node only translates input and formats options.
/// </summary>
public partial class AtlasRouteChoiceController3D : CanvasLayer
{
    public bool ChoiceActive { get; private set; }
    public string SelectedMapId { get; private set; } = string.Empty;
    public int OptionCount => _optionMapIds.Count;
    public IReadOnlyList<string> OptionMapIds => _optionMapIds;
    public int SelectionCount { get; private set; }
    public int ConfirmCount { get; private set; }

    private readonly List<string> _optionMapIds = new();
    private RunSessionNode _run;
    private MapRewardNode3D _rewards;
    private GameFlowController3D _flow;
    private BuildIntermissionController3D _build;
    private Control _panel;
    private Label _optionsLabel;
    private bool _bound;
    private bool _exiting;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _panel = GetNodeOrNull<Control>("RoutePanel");
        _optionsLabel = GetNodeOrNull<Label>("RoutePanel/Options");
        CallDeferred(nameof(BindRuntime));
    }

    public override void _ExitTree()
    {
        _exiting = true;
        if (_bound)
        {
            _run.RouteOptionsChanged -= OnRouteOptionsChanged;
            _rewards.RewardChosen -= OnRewardChosen;
            if (_build != null)
            {
                _build.PhaseChanged -= OnBuildPhaseChanged;
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!ChoiceActive)
        {
            return;
        }

        var index = GetRouteIndex(@event);
        if (index >= 0 && TrySelect(index))
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("next_map", true) && TryConfirm())
        {
            GetViewport().SetInputAsHandled();
        }
    }

    public bool TrySelect(int index)
    {
        if (!ChoiceActive || index < 0 || index >= _optionMapIds.Count)
        {
            return false;
        }

        var mapId = _optionMapIds[index];
        if (!_run.TrySelectNextAtlasMap(mapId))
        {
            return false;
        }

        SelectedMapId = mapId;
        SelectionCount++;
        Refresh();
        return true;
    }

    public bool TryConfirm()
    {
        if (!ChoiceActive || string.IsNullOrWhiteSpace(SelectedMapId))
        {
            return false;
        }

        if (!_run.LoadSelectedMap())
        {
            return false;
        }

        ConfirmCount++;
        ChoiceActive = false;
        SelectedMapId = string.Empty;
        Refresh();
        return true;
    }

    private void BindRuntime()
    {
        if (_exiting || !IsInsideTree())
        {
            return;
        }

        _run = MapRuntimeScope3D.FindRunSession(this);
        _rewards = GetParent<Node3D>()?.GetNodeOrNull<MapRewardNode3D>("MapRewards3D");
        _flow = GetParent<Node3D>()?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        _build = GetParent<Node3D>()?.GetNodeOrNull<BuildIntermissionController3D>("BuildIntermission3D");
        if (_run == null || _rewards == null || _flow == null)
        {
            CallDeferred(nameof(BindRuntime));
            return;
        }

        _run.RouteOptionsChanged += OnRouteOptionsChanged;
        _rewards.RewardChosen += OnRewardChosen;
        if (_build != null)
        {
            _build.PhaseChanged += OnBuildPhaseChanged;
        }
        _bound = true;
        Refresh();
    }

    private void OnRewardChosen(string rewardId)
    {
        ChoiceActive = _build == null || _build.IsRouteChoice;
        SelectedMapId = string.Empty;
        Refresh();
    }

    private void OnRouteOptionsChanged() => Refresh();

    private void OnBuildPhaseChanged(int phase) => Refresh();

    private void Refresh()
    {
        if (_run == null || _rewards == null || _flow == null)
        {
            return;
        }

        _optionMapIds.Clear();
        if (_run.HasFormalAtlas
            && _flow.State == GameFlowState.MapComplete
            && _rewards.HasChosen
            && (_build == null || _build.IsRouteChoice))
        {
            _optionMapIds.AddRange(_run.AvailableAtlasMaps.Select(map => map.Id));
            ChoiceActive = _optionMapIds.Count > 0;
        }
        else
        {
            ChoiceActive = false;
            SelectedMapId = string.Empty;
        }

        if (!ChoiceActive)
        {
            if (GodotObject.IsInstanceValid(_panel))
            {
                _panel.Visible = false;
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(_run.PendingAtlasMapId)
            && _optionMapIds.Contains(_run.PendingAtlasMapId))
        {
            SelectedMapId = _run.PendingAtlasMapId;
        }

        if (GodotObject.IsInstanceValid(_panel))
        {
            _panel.Visible = true;
        }

        if (GodotObject.IsInstanceValid(_optionsLabel))
        {
            var lines = _run.AvailableAtlasMaps.Select((map, index) =>
            {
                var marker = map.Id == SelectedMapId ? "[selected] " : string.Empty;
                var modifier = _run.MapModifierCatalog?.ResolveDefinition(map.MapModifierId);
                var encounter = _run.EncounterCatalog?.ResolveDefinition(map.EncounterId);
                var encounterName = _run.EncounterCatalog?.ResolveDisplayName(map.EncounterId)
                    ?? map.EncounterId;
                var dropLevel = System.Math.Max(
                    map.ItemLevel,
                    MapScaling.ItemLevel(
                        _run.CurrentMapLevel + 1,
                        modifier?.Effects ?? new MapModifierStats()));
                return $"{index + 1}. {marker}{map.Name} · Tier {map.Tier}\n"
                    + $"   {modifier?.Name ?? map.MapModifierId} · {encounterName}\n"
                    + $"   Item Level {dropLevel}\n"
                    + $"   {map.Description}";
            });
            _optionsLabel.Text = "Choose next Atlas route\n"
                + string.Join("\n\n", lines)
                + "\n\n1 / 2 / 3: select    N: confirm";
        }
    }

    private static int GetRouteIndex(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
        {
            return -1;
        }

        return key.Keycode switch
        {
            Key.Key1 => 0,
            Key.Key2 => 1,
            Key.Key3 => 2,
            _ => -1,
        };
    }
}
