using Arpg.Domain;
using Godot;

public partial class ItemDrop : Node2D
{
    [Export] public float Radius { get; set; } = 12.0f;

    private Item _item;
    private Label _label;
    private float _time;
    private bool _collected;

    public Item Item => _item;

    public override void _Ready()
    {
        AddToGroup("item_drops");
        _label = GetNodeOrNull<Label>("Label");
        RefreshLabel();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public void Configure(Item item)
    {
        item.Validate();
        _item = item;
        RefreshLabel();
        QueueRedraw();
    }

    public bool TryCollect(PlayerController player)
    {
        if (_collected || _item == null || player?.Inventory == null || !IsInstanceValid(this))
        {
            return false;
        }

        if (!player.Inventory.TryAddItem(_item))
        {
            return false;
        }

        _collected = true;
        QueueFree();
        return true;
    }

    public override void _Draw()
    {
        var bob = Mathf.Sin(_time * 3.0f) * 2.5f;
        var color = _item?.Rarity switch
        {
            Rarity.Unique => new Color(1.0f, 0.54f, 0.18f, 1.0f),
            Rarity.Magic => new Color(0.30f, 0.62f, 1.0f, 1.0f),
            _ => new Color(0.82f, 0.88f, 0.96f, 1.0f),
        };

        DrawCircle(new Vector2(0.0f, bob), Radius, new Color(color, 0.18f));
        DrawArc(new Vector2(0.0f, bob), Radius, 0.0f, Mathf.Tau, 24, color, 2.0f);
        DrawLine(new Vector2(-Radius * 0.55f, bob), new Vector2(Radius * 0.55f, bob), color, 2.0f);
        DrawLine(new Vector2(0.0f, bob - Radius * 0.55f), new Vector2(0.0f, bob + Radius * 0.55f), color, 2.0f);
    }

    private void RefreshLabel()
    {
        if (_label == null)
        {
            return;
        }

        _label.Text = _item == null ? "DROP" : _item.Name;
    }
}
