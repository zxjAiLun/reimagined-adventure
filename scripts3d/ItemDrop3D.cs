using System;
using Arpg.Domain;
using Godot;

public partial class ItemDrop3D : Node3D
{
    public Item Item { get; private set; }

    private Label3D _label;
    private MeshInstance3D _visual;
    private float _time;
    private Vector3 _visualBasePosition;

    public override void _Ready()
    {
        AddToGroup("item_drops_3d");
        _label = GetNodeOrNull<Label3D>("Label");
        _visual = GetNodeOrNull<MeshInstance3D>("Visual");
        _visualBasePosition = _visual?.Position ?? new Vector3(0.0f, 0.35f, 0.0f);
        RefreshVisuals();
    }

    public void Configure(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Validate();
        Item = item;
        RefreshVisuals();
    }

    public bool TryCollect(PlayerController3D player)
    {
        if (Item == null || player == null || !IsInstanceValid(this))
        {
            return false;
        }

        if (!player.TryAddItem(Item))
        {
            return false;
        }

        QueueFree();
        return true;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        if (_visual != null)
        {
            _visual.Position = _visualBasePosition + Vector3.Up * (Mathf.Sin(_time * 3.0f) * 0.08f);
        }
    }

    private void RefreshVisuals()
    {
        if (_label != null)
        {
            _label.Text = Item?.Name ?? "DROP";
            _label.Modulate = Item?.Rarity switch
            {
                Rarity.Unique => new Color(1.0f, 0.55f, 0.18f),
                Rarity.Magic => new Color(0.35f, 0.65f, 1.0f),
                _ => new Color(0.85f, 0.9f, 0.98f),
            };
        }
    }
}
