using Godot;
using System;

namespace Scripts.Generation;

/// <summary>
/// Contains info on <see cref="GridMap"/> neighbours (Does NOT include <see cref="InteriorObject"/>s).
/// </summary>
public struct NeighbourInfo
{
    public readonly Vector3I Position;
    public readonly Vector3I Direction;
    public readonly ItemManager.Id ItemId;
    public readonly bool Empty;

    public NeighbourInfo(Vector3I position, Vector3I direction, int itemIdx)
    {
        Position = position;
        Direction = direction;
        ItemId = (ItemManager.Id)itemIdx;
        Empty = (ItemId == ItemManager.Id.Empty);
    }

    public static bool GetFirstFilled(NeighbourInfo[] neighbours, out NeighbourInfo filledNeighbour)
    {
        foreach (NeighbourInfo neighbour in neighbours)
        {
            if (!neighbour.Empty)
            {
                filledNeighbour = neighbour;
                return true;
            }
        }
        filledNeighbour = new NeighbourInfo();
        return false;
    }

    public static bool GetFirstEmpty(NeighbourInfo[] neighbours, out NeighbourInfo emptyNeighbour)
    {
        foreach (NeighbourInfo neighbour in neighbours)
        {
            if (neighbour.Empty)
            {
                emptyNeighbour = neighbour;
                return true;
            }
        }
        emptyNeighbour = new NeighbourInfo();
        return false;
    }

    public static int GetEmptyCount(NeighbourInfo[] neighbours)
    {
        int count = 0;
        foreach (NeighbourInfo neighbour in neighbours)
        {
            if (neighbour.Empty) { count++; }
        }
        return count;
    }

    /// <summary>
    /// Rotates contents by offseting all the values, and assigning them to a new array.<para/>
    /// NOTE: Compatible only with arrays containing groups of 4 directions (e.g. Orthogonal/Diagonal, or 3x3/3x3x3).
    /// </summary>
    /// <returns>Offset <see cref="NeighbourInfo"/>[]</returns>
    public static NeighbourInfo[] RotateNeighbours(NeighbourInfo[] neighbours, float rotationY)
    {
        int offset;
        switch (rotationY)
        {
            case Mathf.Pi * 0.5f:
            case -Mathf.Pi * 1.5f:
                offset = 3;
                break;

            case Mathf.Pi:
            case -Mathf.Pi:
                offset = 2;
                break;

            case -Mathf.Pi * 0.5f:
            case Mathf.Pi * 1.5f:
                offset = 1;
                break;

            default: return neighbours;
        }
        int offset0 = (0 + offset) % 4;
        int offset1 = (1 + offset) % 4;
        int offset2 = (2 + offset) % 4;
        int offset3 = (3 + offset) % 4;

        NeighbourInfo[] rotated = new NeighbourInfo[neighbours.Length];
        for (int i = 0; i < rotated.Length; i += 4)
        {
            rotated[i] = neighbours[i + offset0];
            rotated[i + 1] = neighbours[i + offset1];
            rotated[i + 2] = neighbours[i + offset2];
            rotated[i + 3] = neighbours[i + offset3];
        }
        return rotated;
    }
}
