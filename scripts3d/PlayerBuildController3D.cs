using System;
using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

public enum BuildPanelMode
{
    Inventory,
    Stash,
    Supports,
    Passives,
}

/// <summary>
/// Build-facing adapter. Domain inventory/equipment remain authoritative;
/// this node owns only selection, input and the paused presentation state.
/// </summary>
public partial class PlayerBuildController3D : Node
{
    [Signal]
    public delegate void BuildChangedEventHandler();

    public PlayerController3D Player { get; private set; }
    public InventoryScreenController3D Screen { get; private set; }
    public bool IsOpen { get; private set; }
    public BuildPanelMode PanelMode { get; private set; } = BuildPanelMode.Inventory;
    public string SelectedItemId { get; private set; } = string.Empty;
    public string SelectedPassiveNodeId { get; private set; } = string.Empty;
    public RunCurrencyWallet Currency { get; } = new();
    public SkillLoadout SkillLoadout => Player?.Skills?.Loadout;
    public RunSessionNode RunSession => _runSession;
    public IReadOnlyList<PassiveNodeDefinition> PassiveNodes =>
        _runSession?.PassiveTree.Nodes ?? Array.Empty<PassiveNodeDefinition>();

    private bool _previousPaused;
    private bool _exiting;
    private RunSessionNode _runSession;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Player = GetParent<PlayerController3D>();
        _runSession = MapRuntimeScope3D.FindRunSession(this);
        Screen = GetParent()?.GetNodeOrNull<InventoryScreenController3D>("InventoryScreen3D");
        Player.InventoryChanged += OnPlayerBuildChanged;
        Player.EquipmentChanged += OnPlayerBuildChanged;
        if (Player.Skills != null)
        {
            Player.Skills.SkillLoadoutChanged += OnPlayerBuildChanged;
        }
        if (_runSession != null)
        {
            _runSession.CharacterProgressionChanged += OnProgressionChanged;
            _runSession.PassiveAllocationChanged += OnPassiveAllocationChanged;
        }
        SetProcessUnhandledInput(true);
        CallDeferred(nameof(BindScreen));
    }

    public override void _ExitTree()
    {
        _exiting = true;
        if (Player != null)
        {
            Player.InventoryChanged -= OnPlayerBuildChanged;
            Player.EquipmentChanged -= OnPlayerBuildChanged;
            if (Player.Skills != null)
            {
                Player.Skills.SkillLoadoutChanged -= OnPlayerBuildChanged;
            }
        }
        if (_runSession != null)
        {
            _runSession.CharacterProgressionChanged -= OnProgressionChanged;
            _runSession.PassiveAllocationChanged -= OnPassiveAllocationChanged;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("open_inventory", true))
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (!IsOpen)
        {
            return;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.V)
        {
            TogglePassivePanel();
            GetViewport().SetInputAsHandled();
        }
        else if (PanelMode == BuildPanelMode.Passives
            && @event is InputEventKey passiveKey
            && passiveKey.Pressed
            && !passiveKey.Echo
            && passiveKey.Keycode == Key.G)
        {
            TryAllocateSelectedPassive();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("equip_item", true))
        {
            TryEquipSelected();
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventKey inputKey && inputKey.Pressed && !inputKey.Echo)
        {
            if (PanelMode == BuildPanelMode.Passives && inputKey.Keycode == Key.Up)
            {
                SelectPassiveRelative(-1);
                GetViewport().SetInputAsHandled();
            }
            else if (PanelMode == BuildPanelMode.Passives && inputKey.Keycode == Key.Down)
            {
                SelectPassiveRelative(1);
                GetViewport().SetInputAsHandled();
            }
            else if (inputKey.Keycode == Key.Up)
            {
                SelectRelative(-1);
                GetViewport().SetInputAsHandled();
            }
            else if (inputKey.Keycode == Key.Down)
            {
                SelectRelative(1);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public bool Open()
    {
        if (IsOpen || Player == null || !CanManageBuild())
        {
            return false;
        }

        _previousPaused = GetTree().Paused;
        IsOpen = true;
        PanelMode = BuildPanelMode.Inventory;
        GetTree().Paused = true;
        EnsureSelection();
        EnsurePassiveSelection();
        Screen?.ShowBuild(this);
        EmitSignal(SignalName.BuildChanged);
        return true;
    }

    public bool Close()
    {
        if (!IsOpen)
        {
            return false;
        }

        IsOpen = false;
        PanelMode = BuildPanelMode.Inventory;
        Screen?.HideBuild();
        GetTree().Paused = _previousPaused;
        EmitSignal(SignalName.BuildChanged);
        return true;
    }

    public bool SelectItem(string itemId)
    {
        if (!IsOpen || Player?.Items.All(item => item.Id != itemId) != false)
        {
            return false;
        }

        SelectedItemId = itemId;
        EmitSignal(SignalName.BuildChanged);
        return true;
    }

    public bool TryEquipSelected() =>
        !string.IsNullOrWhiteSpace(SelectedItemId) && Player.TryEquipItem(SelectedItemId);

    public bool TryUnequip(EquipmentSlot slot) => Player.TryUnequip(slot);

    public bool TryAttachSupport(SkillSlot slot, string supportId) =>
        Player?.Skills?.TryAttachSupport(slot, supportId) == true;

    public bool TryDetachSupport(SkillSlot slot) =>
        Player?.Skills?.TryDetachSupport(slot) == true;

    public void TogglePassivePanel()
    {
        if (!IsOpen)
        {
            return;
        }

        PanelMode = PanelMode == BuildPanelMode.Passives
            ? BuildPanelMode.Inventory
            : BuildPanelMode.Passives;
        EnsurePassiveSelection();
        EmitSignal(SignalName.BuildChanged);
    }

    public bool TryAllocateSelectedPassive()
    {
        if (!IsOpen
            || PanelMode != BuildPanelMode.Passives
            || _runSession == null
            || string.IsNullOrWhiteSpace(SelectedPassiveNodeId))
        {
            return false;
        }

        var allocated = _runSession.TryAllocatePassive(SelectedPassiveNodeId);
        if (allocated)
        {
            EmitSignal(SignalName.BuildChanged);
        }

        return allocated;
    }

    public string CompareSelectedToSlot()
    {
        var selected = Player?.Items.FirstOrDefault(item => item.Id == SelectedItemId);
        var current = selected == null ? null : Player.Equipment.ItemInSlot(selected.Slot);
        return selected == null
            ? "No item selected"
            : $"{selected.Name}: Damage {selected.Stats.DamageMultiplier - (current?.Stats.DamageMultiplier ?? 1.0):+0.00;-0.00;0.00}, "
                + $"Max HP {selected.Stats.MaxHp - (current?.Stats.MaxHp ?? 0):+0;-0;0}";
    }

    private void BindScreen()
    {
        if (_exiting || !IsInsideTree())
        {
            return;
        }

        Screen ??= GetParent()?.GetNodeOrNull<InventoryScreenController3D>("InventoryScreen3D");
        if (Screen == null)
        {
            CallDeferred(nameof(BindScreen));
            return;
        }

        Screen.ShowBuild(this);
        Screen.HideBuild();
    }

    private void OnPlayerBuildChanged()
    {
        EnsureSelection();
        EmitSignal(SignalName.BuildChanged);
    }

    private void OnProgressionChanged(int level, int totalExperience, int unspentPoints)
    {
        EmitSignal(SignalName.BuildChanged);
    }

    private void OnPassiveAllocationChanged(string nodeId)
    {
        EnsurePassiveSelection();
        EmitSignal(SignalName.BuildChanged);
    }

    private void EnsureSelection()
    {
        if (Player?.Items.Count == 0)
        {
            SelectedItemId = string.Empty;
            return;
        }

        if (Player.Items.All(item => item.Id != SelectedItemId))
        {
            SelectedItemId = Player.Items[0].Id;
        }
    }

    private void SelectRelative(int delta)
    {
        if (Player?.Items.Count == 0)
        {
            return;
        }

        var current = Math.Max(0, Player.Items.ToList().FindIndex(item => item.Id == SelectedItemId));
        var next = (current + delta + Player.Items.Count) % Player.Items.Count;
        SelectedItemId = Player.Items[next].Id;
        EmitSignal(SignalName.BuildChanged);
    }

    private void SelectPassiveRelative(int delta)
    {
        if (PassiveNodes.Count == 0)
        {
            SelectedPassiveNodeId = string.Empty;
            return;
        }

        var current = PassiveNodes.ToList().FindIndex(node => node.Id == SelectedPassiveNodeId);
        current = Math.Max(0, current);
        var next = (current + delta + PassiveNodes.Count) % PassiveNodes.Count;
        SelectedPassiveNodeId = PassiveNodes[next].Id;
        EmitSignal(SignalName.BuildChanged);
    }

    private void EnsurePassiveSelection()
    {
        if (PassiveNodes.Count == 0)
        {
            SelectedPassiveNodeId = string.Empty;
            return;
        }

        if (PassiveNodes.All(node => node.Id != SelectedPassiveNodeId))
        {
            SelectedPassiveNodeId = PassiveNodes[0].Id;
        }
    }

    private bool CanManageBuild()
    {
        var flow = Player?.GetParent()?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        return flow == null || flow.State != GameFlowState.GameOver;
    }
}
