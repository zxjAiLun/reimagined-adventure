using System;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Minimal build screen. Business actions are delegated to
/// PlayerBuildController3D; this node only formats the current state.
/// </summary>
public partial class InventoryScreenController3D : CanvasLayer
{
    public bool IsScreenVisible => Visible;
    public string ItemsText => _itemsLabel?.Text ?? string.Empty;
    public string EquipmentText => _equipmentLabel?.Text ?? string.Empty;
    public string DetailsText => _detailsLabel?.Text ?? string.Empty;

    private PlayerBuildController3D _build;
    private Label _itemsLabel;
    private Label _equipmentLabel;
    private Label _detailsLabel;
    private Label _supportsLabel;
    private Label _hintLabel;
    private bool _exiting;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;
        _itemsLabel = GetNodeOrNull<Label>("Panel/Items");
        _equipmentLabel = GetNodeOrNull<Label>("Panel/Equipment");
        _detailsLabel = GetNodeOrNull<Label>("Panel/Details");
        _supportsLabel = GetNodeOrNull<Label>("Panel/Supports");
        _hintLabel = GetNodeOrNull<Label>("Panel/Hint");
        CallDeferred(nameof(BindBuild));
    }

    public override void _ExitTree()
    {
        _exiting = true;
        if (_build != null)
        {
            _build.BuildChanged -= Refresh;
        }
    }

    public void ShowBuild(PlayerBuildController3D build)
    {
        _build = build;
        Visible = true;
        Refresh();
    }

    public void HideBuild()
    {
        Visible = false;
    }

    public void Refresh()
    {
        if (_build?.Player == null)
        {
            return;
        }

        var player = _build.Player;
        if (_itemsLabel != null)
        {
            _itemsLabel.Text = player.Items.Count == 0
                ? "Inventory: empty"
                : "Inventory:\n" + string.Join(
                    "\n",
                    player.Items.Select((item, index) =>
                        $"{index + 1}. {(item.Id == _build.SelectedItemId ? "> " : "  ")}{FormatItem(item)}"));
        }

        if (_equipmentLabel != null)
        {
            _equipmentLabel.Text = "Equipment:\n"
                + string.Join("\n", Enum.GetValues<EquipmentSlot>().Select(slot =>
                    $"{slot}: {FormatItem(player.Equipment.ItemInSlot(slot))}"));
        }

        if (_detailsLabel != null)
        {
            var selected = player.Items.FirstOrDefault(item => item.Id == _build.SelectedItemId);
            _detailsLabel.Text = selected == null
                ? $"Effective Stats\nDamage x{player.EffectiveStats.DamageMultiplier:0.00}\n"
                    + $"Projectile x{player.EffectiveStats.ProjectileDamageMultiplier:0.00}\n"
                    + $"Area x{player.EffectiveStats.AreaDamageMultiplier:0.00}\n"
                    + $"Max HP +{player.EffectiveStats.MaxHp}"
                : $"Selected\n{FormatItem(selected)}\n{FormatStats(selected.Stats)}";
        }

        if (_supportsLabel != null)
        {
            _supportsLabel.Text = "Skill Supports:\n"
                + string.Join("\n", Enum.GetValues<SkillSlot>().Select(slot =>
                    $"{slot}: {player.Skills?.Supports(slot).FirstOrDefault()?.Name ?? "none"}"));
        }

        if (_hintLabel != null)
        {
            _hintLabel.Text = "I: close   Up/Down: select   E: equip   U: unequip selected slot";
        }
    }

    private void BindBuild()
    {
        if (_exiting || !IsInsideTree())
        {
            return;
        }

        _build = GetParent()?.GetNodeOrNull<PlayerBuildController3D>("PlayerBuildController3D");
        if (_build == null)
        {
            CallDeferred(nameof(BindBuild));
            return;
        }

        _build.BuildChanged += Refresh;
    }

    private static string FormatItem(Item item) => item == null
        ? "empty"
        : $"[{item.RarityName}] {item.Name} (ilvl {item.ItemLevel})";

    private static string FormatStats(Stats stats) =>
        $"Damage x{stats.DamageMultiplier:0.00}, Projectile x{stats.ProjectileDamageMultiplier:0.00}, "
        + $"Area x{stats.AreaDamageMultiplier:0.00}, Max HP +{stats.MaxHp}, Armor +{stats.Armor}";
}
