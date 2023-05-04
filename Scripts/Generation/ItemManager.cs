using Godot;
using System.Collections.Generic;

namespace Scripts.Generation;

public class ItemManager
{
    public enum Id
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

    private class MixedItemIdComparer : EqualityComparer<(Id, Id)>
    {
        public override bool Equals((Id, Id) x, (Id, Id) y)
        {
            return (x.Item1 == y.Item1) && (x.Item2 == y.Item2) ||
                   (x.Item1 == y.Item2) && (x.Item2 == y.Item1);
        }

        public override int GetHashCode((Id, Id) obj) => ((int)obj.Item1) ^ ((int)obj.Item2);
    }

    private readonly Dictionary<(Id, Id), Id> _mixedItems = new(new MixedItemIdComparer());
    private readonly Dictionary<Id, (Id, Id)> _reverseMixedItems = new();

    public ItemManager()
    {
        _mixedItems.Add((Id.White, Id.Orange), Id.WhiteOrange);
        _mixedItems.Add((Id.White, Id.Red), Id.WhiteRed);
        _mixedItems.Add((Id.White, Id.Green), Id.WhiteGreen);
        _mixedItems.Add((Id.White, Id.Blue), Id.WhiteBlue);

        _mixedItems.Add((Id.Orange, Id.Red), Id.OrangeRed);
        _mixedItems.Add((Id.Orange, Id.Green), Id.OrangeGreen);
        _mixedItems.Add((Id.Orange, Id.Blue), Id.OrangeBlue);

        _mixedItems.Add((Id.Red, Id.Green), Id.RedGreen);
        _mixedItems.Add((Id.Red, Id.Blue), Id.RedBlue);

        _mixedItems.Add((Id.Green, Id.Blue), Id.GreenBlue);

        // Create Reverse Lookup //
        foreach (KeyValuePair<(Id, Id), Id> pair in _mixedItems)
        {
            _reverseMixedItems.Add(pair.Value, pair.Key);
        }
    }

    public Id GetMixedId(Id id1, Id id2, out bool reversed)
    {
        reversed = false;
        if (_mixedItems.TryGetValue((id1, id2), out Id mixedId))
        {
            (Id storedId1, Id _) = _reverseMixedItems[mixedId];

            reversed = id1 != storedId1;
            return mixedId;
        }
        else { return id1; }
    }
}