using Arpg.Domain;
using Godot;

/// <summary>
/// Presentation-only item row. It reads already-validated Domain data and
/// never decides whether an item may be equipped.
/// </summary>
public partial class ItemPresentation3D : Label
{
    public Item Item { get; private set; }

    public void SetItem(Item item, bool selected = false)
    {
        Item = item;
        Text = item == null
            ? string.Empty
            : $"[{item.RarityName}] {item.Name} · {item.SlotName} · ilvl {item.ItemLevel}";
        Modulate = item == null
            ? Colors.White
            : RarityColor(item.Rarity, selected);
    }

    private static Color RarityColor(Rarity rarity, bool selected)
    {
        var color = rarity switch
        {
            Rarity.Normal => Colors.White,
            Rarity.Magic => new Color("7db7ff"),
            Rarity.Rare => new Color("fff08a"),
            Rarity.Unique => new Color("ff9d4d"),
            _ => Colors.White,
        };
        return selected ? color.Lightened(0.25f) : color;
    }
}
