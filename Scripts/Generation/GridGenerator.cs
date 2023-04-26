using Godot;
using System;
using System.Collections.Generic;

namespace Scripts.Generation;

public partial class GridGenerator : GridMap
{
    private const int MaximumRoomCount        = 20; // Max where generation will stop
    private const int MaximumIterations       = 5;  // Max number of times to randomly extrude a room
    private const int MaximumExtrusionRetries = 50; // Max number of times to keep randomly sizing extrusions until it fits or runs out
    private const int MaximumOuterWidth       = 5;  // Max outer width of extrusion (applied separately to either side of a doorway)
    private const int MaximumLength           = 10; // Max length of extrusion
    private const int MaximumHeight           = 2;  // Max internal height of a room
    private const int MaximumDoorways         = 3;  // Max number of doorways it can generate
    private const int MaximumInteriorRetries  = 1;  // Max number of times to keep attempting to place an interior object in a cell

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
    private ItemManager _itemManager = new();
    private RoomManager _roomManager;
    private Vector3I[] _adjacentDirections =
    {
        Vector3I.Left,
        Vector3I.Right,
        Vector3I.Forward,
        Vector3I.Back
    };
    private Vector3I[] _interiorDirections =
    {
        Vector3I.Left,
        Vector3I.Right,
        Vector3I.Forward,
        Vector3I.Back,
        new Vector3I(-1, 0, -1), // South West
        new Vector3I(1, 0, 1),   // North East
        new Vector3I(-1, 0, 1),  // North West
        new Vector3I(1, 0, -1)   // South East
    };
    private readonly Vector3 _interiorObjOffset = new Vector3(0.5f, 1f, 0.5f);

    public override void _Ready()
    {
        base._Ready();

        // Initialise //
        _roomManager = new(_random);
        GD.Print(_random.Seed);

        // Generation //
        HashSet<(Vector3I, HashSet<Vector3I>)> doorPos_prevOpenFloorPosS = new(new (Vector3I, HashSet<Vector3I>)[] { (Vector3I.Zero, new()) });    
        int roomCount = 0;

        while (doorPos_prevOpenFloorPosS.Count > 0)
        {
            HashSet<(Vector3I, HashSet<Vector3I>)> doorPos_openFloorPosS = new();
            foreach ((Vector3I doorPos, HashSet<Vector3I> prevOpenFloorPosS) in doorPos_prevOpenFloorPosS)
            {
                if (roomCount == MaximumRoomCount) { continue; }
                
                _roomManager.SelectRandomRoom();

                HashSet<Vector3I> floorPosS = GenerateFloor(doorPos, out Vector3I startDir);
                if (floorPosS == null) { continue; }

                BuildColumn(doorPos, 2, ItemManager.ItemId.Empty); // Clear for doorway

                List<(Vector3I, bool)> potentialDoorPosS = GenerateWallsAndCeiling(floorPosS, prevOpenFloorPosS, out HashSet<Vector3I> openFloorPosS);
                HashSet<(Vector3I, HashSet<Vector3I>)> newDoorPos_openFloorPosS = GenerateDoors(potentialDoorPosS, openFloorPosS);         
                GenerateInterior
                (
                    (doorPos == Vector3I.Zero) ? doorPos + (startDir * 2) : doorPos + startDir,
                    newDoorPos_openFloorPosS,
                    openFloorPosS
                );
                doorPos_openFloorPosS.UnionWith(newDoorPos_openFloorPosS);

                roomCount++;
            }
            doorPos_prevOpenFloorPosS = doorPos_openFloorPosS;
        }

        if (roomCount < MaximumRoomCount) { GD.Print($"Maximum room count wasn't reached. Current: {roomCount}"); }

        // Free Unused Objects //
        _random.Dispose();
        _itemManager = null;
        _roomManager = null;
        _adjacentDirections = null;
        _interiorDirections = null;
    }

    /// <returns><c>HashSet</c> of floor positions.</returns>
    private HashSet<Vector3I> GenerateFloor(Vector3I originPos, out Vector3I startDir)
    {
        // Get Initial Direction //
        if (!NeighbourInfo.GetFirstEmptyNeighbour(GetPlanarAdjacentNeighbours(originPos), out NeighbourInfo emptyNeighbour))
        {
            startDir = Vector3I.Zero;
            return null;
        }
        startDir = emptyNeighbour.Direction;

        Vector3I direction = startDir;
        originPos += direction; // Shift one away from the doorway

        // Extrude A Random Number Of Times //
        int iterations = _random.RandiRange(1, MaximumIterations);
        HashSet<Vector3I> floorPosS = new(); // For walls later

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
                if (!AreaContainsItems(originPos, direction, outerWidth, length, floorPosS))
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
                    floorPosS.Add(newPos);
                }
            }

            if (i == iterations - 1) { break; } // Last loop doesn't need a new direction

            // Choose New Random Direction //
            List<Vector3I> potentialDirs = new(_adjacentDirections);
            potentialDirs.Remove(-direction); // Remove opposite direction

            Vector3I newDirection = potentialDirs[_random.RandiRange(0, potentialDirs.Count - 1)];

            // Move The Origin To A New Edge //
            // (length - 2) & (outerWidth - 1) = Move one into existing floor, (outerWidth + 1) = Move out of existing floor
            // This ensures there's a minimum of a 3 cell width connected directly to previous extrusion, a corridor can be made.
            if (newDirection.Equals(direction))     { originPos += (direction * length) + (perpDir * _random.RandiRange(-(outerWidth - 1), outerWidth - 1)); }
            else if (newDirection.Equals(perpDir))  { originPos += (direction * _random.RandiRange(1, length - 2)) + (perpDir * (outerWidth + 1)); }
            else if (newDirection.Equals(-perpDir)) { originPos += (direction * _random.RandiRange(1, length - 2)) + (-perpDir * (outerWidth + 1)); }
            //

            direction = newDirection;
        }       
        return floorPosS;
    }

    /// <returns><c>List</c> of potential door positions, and whether they are connections to previous rooms.</returns>
    private List<(Vector3I, bool)> GenerateWallsAndCeiling(HashSet<Vector3I> floorPosS, HashSet<Vector3I> prevOpenFloorPosS, out HashSet<Vector3I> openFloorPosS)
    {
        openFloorPosS = new(floorPosS);

        // Build Walls And Store Any Potential Doorways //
        List<(Vector3I, bool)> potentialDoorPos_connecteds = new();
        int height = _random.RandiRange(2, MaximumHeight);

        foreach (Vector3I cellPos in floorPosS)
        {
            NeighbourInfo[] adjNeighbours = GetPlanarAdjacentNeighbours(cellPos);
            int emptyCount = NeighbourInfo.GetEmptyNeighbourCount(adjNeighbours);

            if (NeighbourInfo.GetFirstEmptyNeighbour(adjNeighbours, out NeighbourInfo emptyNeighbour))
            {
                BuildColumn(cellPos, height, _roomManager.WallId);

                // Excludes corners & not having minimum area as potential doorways
                if (emptyCount == 1 && !AreaContainsItems(cellPos + emptyNeighbour.Direction, emptyNeighbour.Direction, MinimumOuterWidth, MinimumLength)) 
                { 
                    potentialDoorPos_connecteds.Add((cellPos, false));
                }
                openFloorPosS.Remove(cellPos);
            }

            // Ceiling
            // SetCellItem(itemPos + (Vector3I.Up * (height + 1)), ItemManager.ItemId.CubeWhite);
        }

        // Fill Inner Corners And Figure Out Mixed Walls & Connections //
        HashSet<Vector3I> innerCornerPosS = new();
        foreach (Vector3I cellPos in openFloorPosS)
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
                ) ||

                // A specific diagonal arrangement needs to filled to ensure minimum 1 item thickness
                (
                    adjEmptyCount == 1 &&
                    (
                        (
                            adjNeighbours[0].ItemIdx == ItemManager.ItemId.Empty &&
                            (diaNeighbours[1].ItemIdx == ItemManager.ItemId.Empty || diaNeighbours[2].ItemIdx == ItemManager.ItemId.Empty)
                        ) ||
                        (
                            adjNeighbours[1].ItemIdx == ItemManager.ItemId.Empty &&
                            (diaNeighbours[2].ItemIdx == ItemManager.ItemId.Empty || diaNeighbours[3].ItemIdx == ItemManager.ItemId.Empty)
                        ) ||
                        (
                            adjNeighbours[2].ItemIdx == ItemManager.ItemId.Empty &&
                            (diaNeighbours[3].ItemIdx == ItemManager.ItemId.Empty || diaNeighbours[0].ItemIdx == ItemManager.ItemId.Empty)
                        ) ||
                        (
                            adjNeighbours[3].ItemIdx == ItemManager.ItemId.Empty &&
                            (diaNeighbours[0].ItemIdx == ItemManager.ItemId.Empty || diaNeighbours[1].ItemIdx == ItemManager.ItemId.Empty)
                        )
                    )
                )
            )
            {
                BuildColumn(cellPos, height, _roomManager.WallId);
                innerCornerPosS.Add(cellPos);
            }
            else // Check if walls need to be mixed
            {
                foreach (NeighbourInfo neighbour in adjNeighbours)
                {
                    Vector3I neighbourfloorPos = neighbour.Position + Vector3I.Down;
                    Vector3I neighbourFloorAheadPos = neighbourfloorPos + neighbour.Direction;

                    if (neighbour.ItemIdx != ItemManager.ItemId.Empty)
                    {
                        if // Walls empty on both sides AND has floor item
                        (
                            GetCellItem(neighbour.Position + neighbour.Direction) == (int)ItemManager.ItemId.Empty &&
                            GetCellItem(neighbourFloorAheadPos) != (int)ItemManager.ItemId.Empty
                        ) 
                        {
                            BuildColumn
                            (
                                neighbourfloorPos,
                                height,
                                _itemManager.GetMixedId(neighbour.ItemIdx, _roomManager.WallId, out bool reversed),
                                GetIndexOfRotationAroundY
                                (
                                    Vector3.Forward,
                                    neighbour.Direction,
                                    Mathf.Pi * (reversed ? 0.5f : -0.5f)
                                )
                            );

                            // Add Potential Connections Into Previous Rooms //
                            Vector3I perpDir = GetPerpendicularVector(neighbour.Direction);

                            if // Make sure walls are either side of the neighbour (avoid double width doors), and isn't from the previous room's floor
                            (
                                !prevOpenFloorPosS.Contains(neighbourFloorAheadPos) &&
                                GetCellItem(neighbour.Position + perpDir) != (int)ItemManager.ItemId.Empty &&
                                GetCellItem(neighbour.Position - perpDir) != (int)ItemManager.ItemId.Empty
                            )
                            { potentialDoorPos_connecteds.Add((neighbourfloorPos, true)); }
                        }
                        else { BuildColumn(neighbourfloorPos, height, _roomManager.WallId); } // Replace existing column (mainly for corners that stick into other rooms)
                    }
                }
            }
        }
        openFloorPosS.ExceptWith(innerCornerPosS);
        return potentialDoorPos_connecteds;
    }

    /// <returns><c>HashSet</c> of door positions paired with the <c>HashSet</c> for the current open floor positions.</returns>
    private HashSet<(Vector3I, HashSet<Vector3I>)> GenerateDoors(List<(Vector3I, bool)> potentialDoorPos_connecteds, HashSet<Vector3I> openFloorPosS)
    {
        HashSet<(Vector3I, HashSet<Vector3I>)> doorPos_openFloorPosS = new();
        int doorCount = _random.RandiRange(1, MaximumDoorways);

        for (int i = 0; i < doorCount; i++)
        {
            if (potentialDoorPos_connecteds.Count == 0) { break; }

            // Create Random Doorway //
            (Vector3I doorPos, bool connected) = potentialDoorPos_connecteds[_random.RandiRange(0, potentialDoorPos_connecteds.Count - 1)];

            if (connected) { BuildColumn(doorPos, 2, ItemManager.ItemId.Empty); }
            else           { doorPos_openFloorPosS.Add((doorPos, openFloorPosS)); }

            potentialDoorPos_connecteds.Remove((doorPos, connected));

            // Remove Adjacent Potential Doorway Positions (doors shouldn't be created right next to each other) //
            NeighbourInfo[] adjNeighbours = GetPlanarAdjacentNeighbours(doorPos);
            foreach (NeighbourInfo neighbour in adjNeighbours)
            {
                if (neighbour.ItemIdx != ItemManager.ItemId.Empty)
                {
                    potentialDoorPos_connecteds.RemoveAll(item => item.Item1 == neighbour.Position);
                }
            }
        }
        return doorPos_openFloorPosS;
    }

    private void GenerateInterior(Vector3I frontOfDoorPos, HashSet<(Vector3I, HashSet<Vector3I>)> doorPos_openFloorPosS, HashSet<Vector3I> openFloorPosS)
    {       
        int[][] cellProximities = new int[openFloorPosS.Count][];
        float[] normaliseVals = new float[] { 2f, 2f, 2f, 2f };

        AStar3D aStar = new();
        Dictionary<Vector3I, long> posToId = new(openFloorPosS.Count);
        Dictionary<long, Vector3I> idToPos = new(openFloorPosS.Count);
        long currentId = 0; // ID to assign to the next unique position

        if (openFloorPosS.Count > aStar.GetPointCapacity()) { aStar.ReserveSpace(openFloorPosS.Count); }       

        int i = -1;
        foreach (Vector3I floorPos in openFloorPosS)
        {
            i++;

            // Find The Directional Proximities //
            cellProximities[i] = new int[_interiorDirections.Length];
            Vector3I upperPos = floorPos + Vector3I.Up;

            for (int j = 0; j < _interiorDirections.Length; j++)
            {
                Vector3I dir = _interiorDirections[j];
                Vector3I move = dir;
                int dist = 0;

                // Is next cell empty and within room bounds
                while (GetCellItem(upperPos + move) == (int)ItemManager.ItemId.Empty && openFloorPosS.Contains(floorPos + move))
                {
                    move = (++dist + 1) * dir;
                }
                cellProximities[i][j] = dist;
            }
            // Retrieve every other cell proximity (only need max in one direction for each axis)
            for (int j = 0; j < normaliseVals.Length; j++) { normaliseVals[j] = Mathf.Max(normaliseVals[j], cellProximities[i][j * 2]); }

            // Setup AStar Nodes & Connections //
            long floorPosId = GetAStarId(posToId, idToPos, floorPos, ref currentId);

            aStar.AddPoint(floorPosId, floorPos);
            foreach (Vector3I dir in _adjacentDirections)
            {
                Vector3I pos = floorPos + dir;
                if (openFloorPosS.Contains(pos))
                {
                    long posId = GetAStarId(posToId, idToPos, pos, ref currentId);

                    aStar.AddPoint(posId, pos);
                    aStar.ConnectPoints(floorPosId, posId);
                }
            }
        }
        // Create Normalise Values For Each Axis //
        for (i = 0; i < normaliseVals.Length; i++) { normaliseVals[i] = 2f / normaliseVals[i]; }

        // Add Paths To Occupied Positions //
        HashSet<Vector3I> occupiedPosS = new();
        long insideOfDoorId = GetAStarId(posToId, idToPos, frontOfDoorPos, ref currentId);

        foreach ((Vector3I otherDoorPos, HashSet<Vector3I> _) in doorPos_openFloorPosS)
        {
            NeighbourInfo.GetFirstEmptyNeighbour(GetPlanarAdjacentNeighbours(otherDoorPos), out NeighbourInfo emptyNeighbour);

            Vector3I insideOfOtherDoorPos = otherDoorPos - emptyNeighbour.Direction;
            if (!openFloorPosS.Contains(insideOfOtherDoorPos)) { insideOfOtherDoorPos -= emptyNeighbour.Direction; } // Due to inner corner corrections, there can sometimes be 2 thick walls.

            long[] path = aStar.GetIdPath
            (
                insideOfDoorId,
                GetAStarId(posToId, idToPos, insideOfOtherDoorPos, ref currentId)
            );
            foreach (long id in path) { occupiedPosS.Add(idToPos[id]); }
        }

        // Proximity Indexes //
        const int Left      = 0;
        const int Right     = 1;
        const int Forward   = 2;
        const int Back      = 3;
        const int SouthWest = 4;
        const int NorthEast = 5;
        const int NorthWest = 6;
        const int SouthEast = 7;

        // Place Objects On Floor Cells //
        i = -1;
        foreach (Vector3I floorPos in openFloorPosS)
        {
            i++;

            if (occupiedPosS.Contains(floorPos)) { continue; }
            int[] cellProx = cellProximities[i];

            // Invalid Placements //
            if
            (   // Checks for being in a 1 width corridor
                ( (cellProx[Left] == 0 && cellProx[Right] == 0) && (cellProx[Forward] > 0 && cellProx[Back] > 0) ) || // EnclosedX & OpenZ
                ( (cellProx[Forward] == 0 && cellProx[Back] == 0) && (cellProx[Left] > 0 && cellProx[Right] > 0) ) || // EnclosedZ & OpenX

                // Checks for two walls with a gap in the middle, in each orientation
                (cellProx[Left] > 0    && cellProx[SouthWest] == 0 && cellProx[NorthWest] == 0) || // Rotation 0
                (cellProx[Back] > 0    && cellProx[NorthWest] == 0 && cellProx[NorthEast] == 0) || // Rotation 90
                (cellProx[Right] > 0   && cellProx[NorthEast] == 0 && cellProx[SouthEast] == 0) || // Rotation 180
                (cellProx[Forward] > 0 && cellProx[SouthEast] == 0 && cellProx[SouthWest] == 0)    // Rotation 270
            )
            { continue; }

            // Get Smallest Axes Proximity With Its Index //
            (float, int) minProx_index = (cellProx[0], 0);
            for (int j = 1; j < cellProx.Length; j++)
            {
                int prox = cellProx[j];
                if (prox < minProx_index.Item1) { minProx_index = (prox, j); }
            }
            minProx_index = (minProx_index.Item1 * normaliseVals[minProx_index.Item2 / 2], minProx_index.Item2); // Normalise the proximity

            // Get Rotation Of The Direction From The Index //
            float rot = 0f;
            switch (minProx_index.Item2)
            {
                case Left:
                case SouthWest:
                case NorthWest:
                    rot = Mathf.Pi * 0.5f;
                    break;

                case Right:
                case SouthEast:
                case NorthEast:
                    rot = Mathf.Pi * -0.5f;
                    break;

                case Forward:
                    break;

                case Back:
                    rot = Mathf.Pi;
                    break;
            }

            // Attempt To Place Objects //
            for (int j = 0; j < MaximumInteriorRetries; j++)
            {
                InteriorObject obj = _roomManager.GetRandomInteriorObject();
                if (obj.Exact)
                {
                    if (obj.WeightToCentre == minProx_index.Item1 && _random.Randf() <= obj.Rarity)
                    {
                        CreateInteriorObject(obj.Scene, floorPos, rot);
                        break;
                    }
                }
                else if (_random.Randf() <= obj.Rarity * GetProximityProbability(obj.WeightToCentre, minProx_index.Item1))
                {
                    CreateInteriorObject(obj.Scene, floorPos, rot);
                    break;
                }
            }
        }
    }

    private void CreateInteriorObject(PackedScene scene, Vector3 floorPos, float rotation)
    {
        Node3D node = scene.Instantiate<Node3D>();

        AddChild(node);
        node.GlobalPosition = floorPos + _interiorObjOffset;
        node.GlobalRotation = new Vector3(0f, rotation, 0f);
    }

    /// <summary>
    /// Tries to get an existing position's ID, otherwise it creates a new entry with an incremented ID.
    /// </summary>
    /// <returns>Associated ID for the given <c>posToCheck</c>.</returns>
    private long GetAStarId(Dictionary<Vector3I, long> posToId, Dictionary<long, Vector3I> idToPos, Vector3I posToCheck, ref long currentId)
    {
        if (posToId.TryGetValue(posToCheck, out long id)) { return id; }
        else
        {
            posToId.Add(posToCheck, currentId);
            idToPos.Add(currentId, posToCheck);

            return currentId++;
        }
    }

    /// <summary>
    /// Calculates probability depending on the weighting and normalised proximity from the edge of a room.
    /// </summary>
    /// <param name="weight">Weight towards the centre between 0 and 1 (inclusive).</param>
    /// <param name="normalisedProx">Normalised proximity from the edge to the centre.</param>
    /// <returns>Probability represented as a float between 0 and 1 (inclusive).</returns>
    private float GetProximityProbability(float weight, float normalisedProx) => (weight * normalisedProx) + ((1 - weight) * (1 - normalisedProx));

    /// <summary>
    /// Get neighbouring cells on each adjacent side, on the y-plane.
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
    /// Get neighbouring cells on each diagonal side (corners), on the y-plane.
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

    /// <returns>Orthogonal index from rotating around the global-y axis.</returns>
    private int GetIndexOfRotationAroundY(Vector3 from, Vector3I to, float angleOffset = 0f)
    {
        float angle = from.SignedAngleTo
        (
            new Vector3(to.X, to.Y, to.Z),
            Vector3.Up
        ) + angleOffset;

        return GetOrthogonalIndexFromBasis(new Basis(Vector3I.Up, angle));
    }

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
    private void BuildColumn(Vector3I floorPos, int height, ItemManager.ItemId itemId, int orientation)
    {
        for (int y = 1; y <= height; y++)
        {
            SetCellItem(floorPos + (Vector3I.Up * y), (int)itemId, orientation);
        }
    }
}
