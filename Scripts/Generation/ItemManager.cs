using Godot;
using System.Collections.Generic;

namespace Scripts.Generation;

public class ItemManager
{
    public enum ItemId
    {
        Empty = -1,
        White,
        Orange,
        Red,
        Green,
        Blue,

        WhiteOrange,
        WhiteRed,
        WhiteGreen,
        WhiteBlue,
        OrangeRed,
        OrangeGreen,
        OrangeBlue,
        RedGreen,
        RedBlue,
        GreenBlue
    }

    private class MixedItemIdComparer : EqualityComparer<(ItemId, ItemId)>
    {
        public override bool Equals((ItemId, ItemId) x, (ItemId, ItemId) y)
        {
            return (x.Item1 == y.Item1) && (x.Item2 == y.Item2) ||
                   (x.Item1 == y.Item2) && (x.Item2 == y.Item1);
        }

        public override int GetHashCode((ItemId, ItemId) obj) => ((int)obj.Item1) ^ ((int)obj.Item2);
    }

    private readonly Dictionary<(ItemId, ItemId), ItemId> _mixedItems = new(new MixedItemIdComparer());
    private readonly Dictionary<ItemId, (ItemId, ItemId)> _reverseMixedItems = new();

    public ItemManager()
    {
        _mixedItems.Add((ItemId.White, ItemId.Orange), ItemId.WhiteOrange);
        _mixedItems.Add((ItemId.White, ItemId.Red), ItemId.WhiteRed);
        _mixedItems.Add((ItemId.White, ItemId.Green), ItemId.WhiteGreen);
        _mixedItems.Add((ItemId.White, ItemId.Blue), ItemId.WhiteBlue);

        _mixedItems.Add((ItemId.Orange, ItemId.Red), ItemId.OrangeRed);
        _mixedItems.Add((ItemId.Orange, ItemId.Green), ItemId.OrangeGreen);
        _mixedItems.Add((ItemId.Orange, ItemId.Blue), ItemId.OrangeBlue);

        _mixedItems.Add((ItemId.Red, ItemId.Green), ItemId.RedGreen);
        _mixedItems.Add((ItemId.Red, ItemId.Blue), ItemId.RedBlue);

        _mixedItems.Add((ItemId.Green, ItemId.Blue), ItemId.GreenBlue);

        // Create Reverse Lookup //
        foreach (KeyValuePair<(ItemId, ItemId), ItemId> pair in _mixedItems)
        {
            _reverseMixedItems.Add(pair.Value, pair.Key);
        }
    }

    public ItemId GetMixedId(ItemId id1, ItemId id2, out bool reversed)
    {
        reversed = false;
        if (_mixedItems.TryGetValue((id1, id2), out ItemId mixedId))
        {
            (ItemId storedId1, ItemId _) = _reverseMixedItems[mixedId];

            reversed = id1 != storedId1;
            return mixedId;
        }
        else { return id1; }
    }
}