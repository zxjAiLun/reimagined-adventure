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
    public string SupportsText => _supportsLabel?.Text ?? string.Empty;
    public string StashText => _stashLabel?.Text ?? string.Empty;
    public string CurrencyText => _currencyLabel?.Text ?? string.Empty;

    private PlayerBuildController3D _build;
    private Label _itemsLabel;
    private Label _equipmentLabel;
    private Label _detailsLabel;
    private Label _supportsLabel;
    private Label _stashLabel;
    private Label _currencyLabel;
    private Label _hintLabel;
    private BuildIntermissionController3D _intermission;
    private bool _exiting;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;
        _itemsLabel = GetNodeOrNull<Label>("Panel/Items");
        _equipmentLabel = GetNodeOrNull<Label>("Panel/Equipment");
        _detailsLabel = GetNodeOrNull<Label>("Panel/Details");
        _supportsLabel = GetNodeOrNull<Label>("Panel/Supports");
        _stashLabel = GetNodeOrNull<Label>("Panel/Stash");
        _currencyLabel = GetNodeOrNull<Label>("Panel/Currency");
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

        if (_intermission != null)
        {
            _intermission.BuildDataChanged -= Refresh;
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
                : $"Selected\n{FormatItem(selected)}\n"
                    + $"Compare\n{_build.CompareSelectedToSlot()}\n"
                    + FormatStats(selected.Stats);
        }

        if (_supportsLabel != null)
        {
            _supportsLabel.Text = "Skill Supports:\n"
                + string.Join("\n", Enum.GetValues<SkillSlot>().Select(slot =>
                    $"{slot}: {player.Skills?.Supports(slot).FirstOrDefault()?.Name ?? "none"}"));
        }

        if (_intermission != null && _stashLabel != null)
        {
            _stashLabel.Text = "Stash:\n"
                + (_intermission.StashItems.Count == 0
                    ? "empty"
                    : string.Join("\n", _intermission.StashItems.Select(item =>
                        $"{(item.Id == _intermission.SelectedStashItemId ? "> " : "  ")}{FormatItem(item)}")));
        }

        if (_intermission != null && _currencyLabel != null)
        {
            _currencyLabel.Text = $"Forge Fragments: {_intermission.Currency.ForgeFragments}";
        }

        if (_hintLabel != null)
        {
            _hintLabel.Text = "I close  Up/Down select  E equip  X unequip  T stash  C reforge  O/P support  B done";
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

        _intermission = GetParent()?.GetParent()?.GetNodeOrNull<BuildIntermissionController3D>("BuildIntermission3D");
        _build.BuildChanged += Refresh;
        if (_intermission != null)
        {
            _intermission.BuildDataChanged += Refresh;
        }
    }

    private static string FormatItem(Item item) => item == null
        ? "empty"
        : $"[{item.RarityName}] {item.Name} (ilvl {item.ItemLevel})";

    private static string FormatStats(Stats stats) =>
        $"Damage x{stats.DamageMultiplier:0.00}, Projectile x{stats.ProjectileDamageMultiplier:0.00}, "
        + $"Area x{stats.AreaDamageMultiplier:0.00}, Max HP +{stats.MaxHp}, Armor +{stats.Armor}";
}
