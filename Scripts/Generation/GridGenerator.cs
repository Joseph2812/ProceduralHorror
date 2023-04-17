using Godot;
using System.Collections.Generic;

namespace Scripts.Generation;

public partial class GridGenerator : GridMap
{
    private const int MaximumRoomCount        = 50; // Max where generation will stop
    private const int MaximumIterations       = 5;  // Max number of times to randomly extrude a room
    private const int MaximumExtrusionRetries = 25; // Max number of times to keep randomly sizing extrusions until it fits or runs out
    private const int MaximumOuterWidth       = 5;  // Max outer width of extrusion (applied separately to either side of a doorway)
    private const int MaximumLength           = 10; // Max length of extrusion
    private const int MaximumHeight           = 2;  // Max internal height of a room
    private const int MaximumDoorways         = 3;  // Max number of doorways it can generate

    private const int MinimumOuterWidth = 1; // Minimum width: (1 * 2) + 1 = 3
    private const int MinimumLength     = 3;

    private struct NeighbourInfo
    {
        public Vector3I Position;
        public Vector3I Direction;
        public ItemManager.ItemId ItemIdx;

        public static bool GetFirstFilledNeighbour(NeighbourInfo[] neighbours, out NeighbourInfo filledNeighbour)
        {
            foreach (NeighbourInfo neighbour in neighbours)
            {
                if (neighbour.ItemIdx != ItemManager.ItemId.Empty)
                {
                    filledNeighbour = neighbour;
                    return true;
                }
            }
            filledNeighbour = new NeighbourInfo();
            return false;
        }

        public static bool GetFirstEmptyNeighbour(NeighbourInfo[] neighbours, out NeighbourInfo emptyNeighbour)
        {
            foreach (NeighbourInfo neighbour in neighbours)
            {
                if (neighbour.ItemIdx == ItemManager.ItemId.Empty)
                {
                    emptyNeighbour = neighbour;
                    return true;
                }
            }
            emptyNeighbour = new NeighbourInfo();
            return false;
        }

        public static int GetEmptyNeighbourCount(NeighbourInfo[] neighbours)
        {
            int count = 0;
            foreach (NeighbourInfo neighbour in neighbours)
            {
                if (neighbour.ItemIdx == ItemManager.ItemId.Empty) { count++; }
            }
            return count;
        }
    }

    private RandomNumberGenerator _random = new();
    private Vector3I[] _extrusionDirections =
    {
        Vector3I.Left,
        Vector3I.Right,
        Vector3I.Forward,
        Vector3I.Back
    };
    private ItemManager _itemManager = new();
    private RoomManager _roomManager;

    public override void _Ready()
    {
        base._Ready();

        // Initialise //
        _roomManager = new(_random);
        _random.Seed = 5333481107181938514;
        GD.Print(_random.Seed);

        // Generation //
        HashSet<Vector3I> doorwayPosSet = new(new Vector3I[] { Vector3I.Zero });     
        int roomCount = 0;

        while (doorwayPosSet.Count > 0)
        {
            HashSet<Vector3I> newDoorwayPosSet = new();
            foreach (Vector3I doorwayPos in doorwayPosSet)
            {
                if (roomCount == MaximumRoomCount) { continue; }
                
                _roomManager.SelectRandomRoom();

                HashSet<Vector3I> floorPosSet = GenerateFloor(doorwayPos);
                if (floorPosSet == null) { continue; }

                BuildColumn(doorwayPos, 2, ItemManager.ItemId.Empty); // Clear doorway

                List<Vector3I> potentialDoorPosList = GenerateWallsAndCeiling(floorPosSet, out HashSet<Vector3I> openFloorPosSet);
                newDoorwayPosSet.UnionWith(GenerateDoorways(potentialDoorPosList));          
                GenerateInterior(openFloorPosSet);

                roomCount++;
            }
            doorwayPosSet = newDoorwayPosSet;
        }

        if (roomCount < MaximumRoomCount) { GD.Print($"Maximum room count wasn't reached. Current: {roomCount}"); }

        // Free Unused Objects //
        _random.Dispose();
        _extrusionDirections = null;
        _itemManager = null;
        _roomManager = null;
    }

    /// <returns>HashSet of floor positions.</returns>
    private HashSet<Vector3I> GenerateFloor(Vector3I originPos)
    {
        // Get Initial Direction //
        if (!NeighbourInfo.GetFirstEmptyNeighbour(GetPlanarAdjacentNeighbours(originPos), out NeighbourInfo emptyNeighbour))
        {
            return null;
        }
        Vector3I direction = emptyNeighbour.Direction;
        originPos += direction; // Shift one away from the doorway

        // Extrude A Random Number Of Times //
        int iterations = _random.RandiRange(1, MaximumIterations);
        HashSet<Vector3I> floorPosSet = new(); // For walls later

        for (int i = 0; i < iterations; i++)
        {
            Vector3I perpDir = GetPerpendicularVector(direction);
            int outerWidth = 0;
            int length = 0;

            // Keep Attempting Random Extrusions //
            bool extrusionFits = false;
            for (int j = 0; j < MaximumExtrusionRetries; j++)
            {
                outerWidth = _random.RandiRange(MinimumOuterWidth, MaximumOuterWidth);
                length = _random.RandiRange(MinimumLength, MaximumLength);

                // Check If The Area Is Clear //
                if (!AreaContainsItems(originPos, direction, outerWidth, length, floorPosSet))
                {
                    extrusionFits = true;
                    break;
                }
            }

            if (!extrusionFits)
            {
                if (i == 0) { return null; }
                else        { break; }
            }

            // Extrude Floor In Direction //
            for (int z = 0; z < length; z++)
            {
                for (int x = -outerWidth; x <= outerWidth; x++)
                {
                    Vector3I newPos = originPos + (direction * z) + (perpDir * x);

                    SetCellItem(newPos, (int)_roomManager.FloorId);
                    floorPosSet.Add(newPos);
                }
            }

            if (i == iterations - 1) { break; } // Last loop doesn't need a new direction

            // Choose New Random Direction //
            List<Vector3I> potentialDir = new(_extrusionDirections);
            potentialDir.Remove(-direction); // Remove opposite direction

            Vector3I newDirection = potentialDir[_random.RandiRange(0, potentialDir.Count - 1)];

            // Move The Origin To A New Edge //
            // (length - 2) & (outerWidth - 1) = Move one into existing floor, (outerWidth + 1) = Move out of existing floor
            // This ensures there's a minimum of a 3 cell width connected directly to previous extrusion, a corridor can be made.
            if (newDirection.Equals(direction))     { originPos += (direction * length) + (perpDir * _random.RandiRange(-(outerWidth - 1), outerWidth - 1)); }
            else if (newDirection.Equals(perpDir))  { originPos += (direction * _random.RandiRange(1, length - 2)) + (perpDir * (outerWidth + 1)); }
            else if (newDirection.Equals(-perpDir)) { originPos += (direction * _random.RandiRange(1, length - 2)) + (-perpDir * (outerWidth + 1)); }
            //

            direction = newDirection;
        }       
        return floorPosSet;
    }

    /// <returns>List of potential door positions.</returns>
    private List<Vector3I> GenerateWallsAndCeiling(HashSet<Vector3I> floorPosSet, out HashSet<Vector3I> openFloorPosSet)
    {
        openFloorPosSet = new(floorPosSet);

        // Build Walls And Store Any Potential Doorways //
        List<Vector3I> potentialDoorwayPosList = new();
        int height = _random.RandiRange(2, MaximumHeight);

        foreach (Vector3I cellPos in floorPosSet)
        {
            NeighbourInfo[] adjNeighbours = GetPlanarAdjacentNeighbours(cellPos);
            int emptyCount = NeighbourInfo.GetEmptyNeighbourCount(adjNeighbours);

            if (NeighbourInfo.GetFirstEmptyNeighbour(adjNeighbours, out NeighbourInfo emptyNeighbour))
            {
                BuildColumn(cellPos, height, _roomManager.WallId);

                // Excludes corners & not having minimum area as potential doorways
                if (emptyCount == 1 && !AreaContainsItems(cellPos + emptyNeighbour.Direction, emptyNeighbour.Direction, MinimumOuterWidth, MinimumLength)) 
                { 
                    potentialDoorwayPosList.Add(cellPos);
                }
                openFloorPosSet.Remove(cellPos);
            }

            // Ceiling
            // SetCellItem(itemPos + (Vector3I.Up * (height + 1)), ItemManager.ItemId.CubeWhite);
        }

        // Fill Inner Corners And Figure Out Mixed Walls //
        HashSet<Vector3I> innerCornerPosSet = new();
        foreach (Vector3I cellPos in openFloorPosSet)
        {
            Vector3I upperPos = cellPos + Vector3I.Up;
            NeighbourInfo[] adjNeighbours = GetPlanarAdjacentNeighbours(upperPos);
            NeighbourInfo[] diaNeighbours = GetPlanarDiagonalNeighbours(upperPos);
            int adjEmptyCount = NeighbourInfo.GetEmptyNeighbourCount(adjNeighbours);

            if
            (   // Is an inner corner
                (
                    NeighbourInfo.GetEmptyNeighbourCount(diaNeighbours) >= 3 &&
                    // Empty adjacent neighbours are NOT opposite each other
                    !(
                        (adjNeighbours[0].ItemIdx == ItemManager.ItemId.Empty && adjNeighbours[2].ItemIdx == ItemManager.ItemId.Empty) ||
                        (adjNeighbours[1].ItemIdx == ItemManager.ItemId.Empty && adjNeighbours[3].ItemIdx == ItemManager.ItemId.Empty)
                    )
                )
                ||
                // A specific diagonal arrangement needs to filled to ensure minimum 1 item thickness
                (
                    adjEmptyCount == 1 &&
                    (
                        (
                            adjNeighbours[0].ItemIdx == ItemManager.ItemId.Empty &&
                            (diaNeighbours[1].ItemIdx == ItemManager.ItemId.Empty || diaNeighbours[2].ItemIdx == ItemManager.ItemId.Empty)
                        )
                        ||
                        (
                            adjNeighbours[1].ItemIdx == ItemManager.ItemId.Empty &&
                            (diaNeighbours[2].ItemIdx == ItemManager.ItemId.Empty || diaNeighbours[3].ItemIdx == ItemManager.ItemId.Empty)
                        )
                        ||
                        (
                            adjNeighbours[2].ItemIdx == ItemManager.ItemId.Empty &&
                            (diaNeighbours[3].ItemIdx == ItemManager.ItemId.Empty || diaNeighbours[0].ItemIdx == ItemManager.ItemId.Empty)
                        )
                        ||
                        (
                            adjNeighbours[3].ItemIdx == ItemManager.ItemId.Empty &&
                            (diaNeighbours[0].ItemIdx == ItemManager.ItemId.Empty || diaNeighbours[1].ItemIdx == ItemManager.ItemId.Empty)
                        )
                    )
                )
            )
            {
                BuildColumn(cellPos, height, _roomManager.WallId);
                innerCornerPosSet.Add(cellPos);
            }
            else // Check if walls need to be mixed
            {
                foreach (NeighbourInfo neighbour in adjNeighbours)
                {
                    Vector3I floorPos = neighbour.Position + Vector3I.Down;

                    if (neighbour.ItemIdx != ItemManager.ItemId.Empty)
                    {
                        if (GetCellItem(neighbour.Position + neighbour.Direction) == (int)ItemManager.ItemId.Empty) // Empty on both sides
                        {
                            BuildColumn
                            (
                                floorPos,
                                height,
                                _itemManager.GetMixedId(neighbour.ItemIdx, _roomManager.WallId, out bool reversed),
                                new Basis
                                (
                                    // Rotation From Global-Z To Direction Of Neighbour //
                                    Vector3I.Up,
                                    Vector3.Forward.SignedAngleTo
                                    (
                                        new Vector3(neighbour.Direction.X, neighbour.Direction.Y, neighbour.Direction.Z),
                                        Vector3.Up
                                    ) + (Mathf.Pi * (reversed ? 0.5f : -0.5f))
                                )
                            );
                        }
                        else { BuildColumn(floorPos, height, _roomManager.WallId); } // Replace existing column (mainly for corners that stick into other rooms)
                    }
                }
            }
        }
        openFloorPosSet.ExceptWith(innerCornerPosSet);

        return potentialDoorwayPosList;
    }

    /// <returns>Set of doorway positions.</returns>
    private HashSet<Vector3I> GenerateDoorways(List<Vector3I> potentialDoorPosList)
    {
        HashSet<Vector3I> doorwayPosSet = new();
        int doorCount = _random.RandiRange(1, MaximumDoorways);

        for (int i = 0; i < doorCount; i++)
        {
            if (potentialDoorPosList.Count == 0) { break; }

            // Create Random Doorway //
            Vector3I doorPos = potentialDoorPosList[_random.RandiRange(0, potentialDoorPosList.Count - 1)];
            doorwayPosSet.Add(doorPos);

            // Remove Adjacent Potential Door Positions (doors shouldn't be created right next to each other) //
            NeighbourInfo[] adjNeighbours = GetPlanarAdjacentNeighbours(doorPos);
            foreach (NeighbourInfo neighbour in adjNeighbours)
            {
                if (neighbour.ItemIdx != ItemManager.ItemId.Empty)
                {
                    potentialDoorPosList.Remove(neighbour.Position);
                }
            }
        }
        return doorwayPosSet;
    }

    private void GenerateInterior(HashSet<Vector3I> openFloorPos)
    {

    }

    /// <summary>
    /// Get neighbouring cells on each adjacent side.
    /// </summary>
    /// <param name="centrePos">Position to find neighbours from.</param>
    /// <returns><c>NeighbourInfo[4] {Left, Forward, Right, Back}</c></returns>
    private NeighbourInfo[] GetPlanarAdjacentNeighbours(Vector3I centrePos)
    {
        NeighbourInfo[] neighbours = new NeighbourInfo[4];
        Vector3I pos;

        pos = centrePos + Vector3I.Left;
        neighbours[0] = new NeighbourInfo
        {
            Position = pos,
            Direction = Vector3I.Left,
            ItemIdx = (ItemManager.ItemId)GetCellItem(pos)
        };

        pos = centrePos + Vector3I.Forward;
        neighbours[1] = new NeighbourInfo
        {
            Position = pos,
            Direction = Vector3I.Forward,
            ItemIdx = (ItemManager.ItemId)GetCellItem(pos)
        };

        pos = centrePos + Vector3I.Right;
        neighbours[2] = new NeighbourInfo
        {
            Position = pos,
            Direction = Vector3I.Right,
            ItemIdx = (ItemManager.ItemId)GetCellItem(pos)
        };

        pos = centrePos + Vector3I.Back;
        neighbours[3] = new NeighbourInfo
        {
            Position = pos,
            Direction = Vector3I.Back,
            ItemIdx = (ItemManager.ItemId)GetCellItem(pos)
        };

        return neighbours;
    }
    ///
    /// <summary>
    /// Get neighbouring cells on each diagonal side (corners).
    /// </summary>
    /// <param name="centrePos">Position to find neighbours from.</param>
    /// <returns><c>NeighbourInfo[4] {NorthWest, NorthEast, SouthEast, SouthWest}</c></returns>
    private NeighbourInfo[] GetPlanarDiagonalNeighbours(Vector3I centrePos)
    {
        NeighbourInfo[] neighbours = new NeighbourInfo[4];
        Vector3I dir;
        Vector3I pos;

        dir = new Vector3I(-1, 0, 1); // North West
        pos = centrePos + dir;
        neighbours[0] = new NeighbourInfo
        {
            Position = pos,
            Direction = dir,
            ItemIdx = (ItemManager.ItemId)GetCellItem(pos)
        };

        dir = new Vector3I(1, 0, 1); // North East
        pos = centrePos + dir;
        neighbours[1] = new NeighbourInfo
        {
            Position = pos,
            Direction = dir,
            ItemIdx = (ItemManager.ItemId)GetCellItem(pos)
        };

        dir = new Vector3I(1, 0, -1); // South East
        pos = centrePos + dir;
        neighbours[2] = new NeighbourInfo
        {
            Position = pos,
            Direction = dir,
            ItemIdx = (ItemManager.ItemId)GetCellItem(pos)
        };

        dir = new Vector3I(-1, 0, -1); // South West
        pos = centrePos + dir;
        neighbours[3] = new NeighbourInfo
        {
            Position = pos,
            Direction = dir,
            ItemIdx = (ItemManager.ItemId)GetCellItem(pos)
        };

        return neighbours;
    }

    /// <returns>Perpendicular vector on the y-plane (90 degrees clockwise).</returns>
    private Vector3I GetPerpendicularVector(Vector3I direction) => new(direction.Z, 0, -direction.X);

    /// <summary>
    /// Iterates through an area of the <c>GridMap</c> to check for any non-empty cells.
    /// </summary>
    /// <param name="startPos">Bottom centre of rectangular area, relative to the <c>direction</c>.</param>
    /// <param name="direction">Direction to iterate along the length of.</param>
    /// <param name="outerWidth">Number of cells either side of a centre cell.</param>
    /// <param name="length">Number of cells along the <c>direction</c>.</param>
    /// <returns>If the area of cells contains any items.</returns>
    private bool AreaContainsItems(Vector3I startPos, Vector3I direction, int outerWidth, int length)
    {
        Vector3I perpDir = GetPerpendicularVector(direction);
        for (int z = 0; z < length; z++)
        {
            for (int x = -outerWidth; x <= outerWidth; x++)
            {
                Vector3I pos = startPos + (direction * z) + (perpDir * x);
                if (GetCellItem(pos) != (int)ItemManager.ItemId.Empty)
                {
                    return true;
                }
            }
        }
        return false;
    }
    ///
    /// <param name="setToExclude">Set of cells to ignore if it is detected in the area.</param>
    private bool AreaContainsItems(Vector3I startPos, Vector3I direction, int outerWidth, int length, HashSet<Vector3I> setToExclude)
    {
        Vector3I perpDir = GetPerpendicularVector(direction);
        for (int z = 0; z < length; z++)
        {
            for (int x = -outerWidth; x <= outerWidth; x++)
            {
                Vector3I pos = startPos + (direction * z) + (perpDir * x);
                if (GetCellItem(pos) != (int)ItemManager.ItemId.Empty && !setToExclude.Contains(pos))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Build a column of items above the floor position.
    /// </summary>
    /// <param name="floorPos">Column will be built ONE above (global-y) this position.</param>
    /// <param name="height">Number of cells from the <c>floorPos</c>.</param>
    /// <param name="itemId">Item to set in each cell.</param>
    private void BuildColumn(Vector3I floorPos, int height, ItemManager.ItemId itemId)
    {
        for (int y = 1; y <= height; y++)
        {
            SetCellItem(floorPos + (Vector3I.Up * y), (int)itemId);
        }
    }
    ///
    /// <param name="orientation">Rotation to apply to each cell, later approximated as discrete indexes.</param>
    private void BuildColumn(Vector3I floorPos, int height, ItemManager.ItemId itemId, Basis orientation)
    {
        for (int y = 1; y <= height; y++)
        {
            SetCellItem(floorPos + (Vector3I.Up * y), (int)itemId, GetOrthogonalIndexFromBasis(orientation));
        }
    }
}
