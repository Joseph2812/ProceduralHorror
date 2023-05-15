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

    private readonly Dictionary<(Id, Id), Id> _mixedItems = new(new MixedItemIdComparer())
    {
        [(Id.White, Id.Orange)] = Id.WhiteOrange,
        [(Id.White, Id.Red)]    = Id.WhiteRed,
        [(Id.White, Id.Green)]  = Id.WhiteGreen,
        [(Id.White, Id.Blue)]   = Id.WhiteBlue,

        [(Id.Orange, Id.Red)]   = Id.OrangeRed,
        [(Id.Orange, Id.Green)] = Id.OrangeGreen,
        [(Id.Orange, Id.Blue)]  = Id.OrangeBlue,

        [(Id.Red, Id.Green)] = Id.RedGreen,
        [(Id.Red, Id.Blue)]  = Id.RedBlue,

        [(Id.Green, Id.Blue)] = Id.GreenBlue
    };
    private readonly Dictionary<Id, (Id, Id)> _reverseMixedItems = new();

    public ItemManager()
    {
        // Create Reverse Lookup //
        foreach (KeyValuePair<(Id, Id), Id> pair in _mixedItems)
        {
            _reverseMixedItems.Add(pair.Value, pair.Key);
        }
    }

    /// <summary>
    /// Find the mixed equivalent of two item IDs, and whether it matches the order in the stored dictionary (<paramref name="mixedId"/> = <paramref name="id2"/> on failure).
    /// </summary>
    /// <param name="id1">1st item ID.</param>
    /// <param name="id2">2nd item ID (<paramref name="mixedId"/> set as this if it fails).</param>
    /// <param name="mixedId">The id mix between <paramref name="id1"/> and <paramref name="id2"/>, if it fails it's just <paramref name="id2"/>.</param>
    /// <param name="reversed">Reversed if it doesn't match the order stored in the dictionary.</param>
    /// <returns>Whether it was a success (failure if one the IDs is already mixed, or IDs are the same).</returns>
    public bool GetMixedId(Id id1, Id id2, out Id mixedId, out bool reversed)
    {      
        if (_mixedItems.TryGetValue((id1, id2), out mixedId))
        {
            reversed = id1 != _reverseMixedItems[mixedId].Item1;
            return true;
        }
        else
        {
            mixedId = id2;
            reversed = false;
            return false;
        }
    }
}