using System;
using System.Linq;
using Arpg.Domain;
using Godot;

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
    public string SelectedItemId { get; private set; } = string.Empty;
    public RunCurrencyWallet Currency { get; } = new();
    public SkillLoadout SkillLoadout => Player?.Skills?.Loadout;

    private bool _previousPaused;
    private bool _exiting;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Player = GetParent<PlayerController3D>();
        Screen = GetParent()?.GetNodeOrNull<InventoryScreenController3D>("InventoryScreen3D");
        Player.InventoryChanged += OnPlayerBuildChanged;
        Player.EquipmentChanged += OnPlayerBuildChanged;
        if (Player.Skills != null)
        {
            Player.Skills.SkillLoadoutChanged += OnPlayerBuildChanged;
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

        if (@event.IsActionPressed("equip_item", true))
        {
            TryEquipSelected();
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.Up)
            {
                SelectRelative(-1);
                GetViewport().SetInputAsHandled();
            }
            else if (key.Keycode == Key.Down)
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
        GetTree().Paused = true;
        EnsureSelection();
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

    private bool CanManageBuild()
    {
        var flow = Player?.GetParent()?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        return flow == null || flow.State != GameFlowState.GameOver;
    }
}
