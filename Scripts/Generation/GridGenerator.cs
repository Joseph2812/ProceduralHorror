using Godot;
using System;
using System.Collections.Generic;

namespace Scripts.Generation;

public partial class GridGenerator : GridMap
{
    private const int MaximumRoomCount        = 30; // Max where generation will stop
    private const int MaximumIterations       = 5;  // Max number of times to randomly extrude a room
    private const int MaximumExtrusionRetries = 50; // Max number of times to keep randomly sizing extrusions until it fits or runs out
    private const int MaximumOuterWidth       = 5;  // Max outer width of extrusion (applied separately to either side of a doorway)
    private const int MaximumLength           = 10; // Max length of extrusion
    private const int MaximumHeight           = 5;  // Max height of a room (including ceiling)
    private const int MaximumDoorways         = 5;  // Max number of doorways it can generate
    private const int MaximumInteriorRetries  = 1;  // Max number of times to keep attempting to place an interior object in a cell  

    private const int MinimumOuterWidth = 1; // Minimum width: (1 * 2) + 1 = 3
    private const int MinimumLength     = 3;

    private const string InteriorObjectParentName = "InteriorObjects";

    private enum OrthDir
    {
        Left,
        Forward,
        Right,
        Back
    }
    private enum DiagDir
    {
        NW,
        NE,
        SE,
        SW
    }
    private enum AllDir
    {
        Left,
        Forward,
        Right,
        Back,
        NW,
        NE,
        SE,
        SW
    }

    private struct NeighbourInfo
    {
        public Vector3I Position     { get; private set; }
        public Vector3I Direction    { get; private set; }
        public ItemManager.Id ItemId { get; private set; }
        public bool Empty            { get; private set; }

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
    }

    private RandomNumberGenerator _random = new();
    private ItemManager _itemManager = new();
    private RoomManager _roomManager;
    private Node _interiorNodeParent;
    private readonly Vector3 _interiorNodeOffset = new Vector3(0f, 1f, 0f);

    private Vector3I[] _orthogonalDirs =
    {
        Vector3I.Left,
        Vector3I.Forward,
        Vector3I.Right,
        Vector3I.Back
    };
    private Vector3I[] _diagonalDirs =
    {
        new Vector3I(-1, 0, -1), // North West (North = -Z)
        new Vector3I(1, 0, -1),  // North East
        new Vector3I(1, 0, 1),   // South East
        new Vector3I(-1, 0, 1)   // South West
    };
    private Vector3I[] _allDirs; // Orthogonal + Diagonal

    public override void _Ready()
    {
        base._Ready();

        // Initialise //
        _roomManager = new(_random);

        _interiorNodeParent = new Node3D { Name = InteriorObjectParentName };
        AddChild(_interiorNodeParent);

        _allDirs = new Vector3I[_orthogonalDirs.Length + _diagonalDirs.Length];
        _orthogonalDirs.CopyTo(_allDirs, 0);
        _diagonalDirs.CopyTo(_allDirs, _orthogonalDirs.Length);

        //_random.Seed = 13224076068604078681;
        GD.Print(_random.Seed);

        bool success = false;
        while (!success) { success = StartGeneration(); }

        // Free Unused Objects //
        _random.Dispose();
        _itemManager = null;
        _roomManager = null;
        _orthogonalDirs = null;
        _diagonalDirs = null;
        _allDirs = null;
    }

    private bool StartGeneration()
    {
        HashSet<Vector3I> prevDoorPosS = new(new Vector3I[] { Vector3I.Zero });
        HashSet<Vector3I> allDoorPosS = new();
        int roomCount = 0;

        while (prevDoorPosS.Count > 0)
        {
            HashSet<Vector3I> doorPosS = new();
            foreach (Vector3I doorPos in prevDoorPosS)
            {
                if (roomCount == MaximumRoomCount)
                {
                    doorPosS.Clear();
                    break;
                }
                _roomManager.SelectRandomRoom();

                HashSet<Vector3I> floorPosS = GenerateFloor(doorPos, out Vector3I startDir);
                if (floorPosS == null) { continue; }

                Vector3I originDoorAheadPos = (doorPos == Vector3I.Zero) ? doorPos + (startDir * 2) : doorPos + startDir;
                int height = _random.RandiRange(3, MaximumHeight);

                List<Vector3I> potentialDoorPosS = GenerateWallsAndCeiling(floorPosS, height);
                HashSet<Vector3I> connectionPosS = MixWallsAndFindConnections(floorPosS, originDoorAheadPos, height);
                HashSet<Vector3I> newDoorPosS = GenerateDoors(potentialDoorPosS);

                allDoorPosS.UnionWith(newDoorPosS);
                doorPosS.UnionWith(newDoorPosS);

                newDoorPosS.UnionWith(connectionPosS); // Add doors from other rooms (Connections)
                GenerateInterior
                (
                    floorPosS,
                    newDoorPosS,
                    originDoorAheadPos
                );
                roomCount++;
            }
            prevDoorPosS = doorPosS;
        }

        if (roomCount < MaximumRoomCount)
        {
            GD.Print($"Maximum room count wasn't reached. Current: {roomCount}. Retrying...");

            Clear();
            _interiorNodeParent.Free();
            _interiorNodeParent = new Node3D { Name = InteriorObjectParentName };
            AddChild(_interiorNodeParent);

            return false;
        }

        // Fill Any Doors Open To The VOID Or Leading Into A Wall (due to corners) //
        foreach (Vector3I pos in allDoorPosS)
        {
            if
            (
                NeighbourInfo.GetFirstEmpty(GetNeighbours(pos, _orthogonalDirs), out _) ||
                NeighbourInfo.GetFirstEmpty(GetNeighbours(pos, _diagonalDirs), out _)
            )
            {
                Vector3I aboveDoor = pos + (Vector3I.Up * 3);
                BuildColumn(pos, 2, (ItemManager.Id)GetCellItem(aboveDoor), GetCellItemOrientation(aboveDoor));
            }
        }
        return true;
    }

    /// <returns><c>HashSet</c> of floor positions.</returns>
    private HashSet<Vector3I> GenerateFloor(Vector3I originPos, out Vector3I startDir)
    {
        // Get Initial Direction //
        if (!NeighbourInfo.GetFirstEmpty(GetNeighbours(originPos, _orthogonalDirs), out NeighbourInfo emptyNeighbour))
        {
            startDir = Vector3I.Zero;
            return null;
        }
        startDir = emptyNeighbour.Direction;

        // Extrude A Random Number Of Times //
        HashSet<Vector3I> floorPosS = new(); // For walls later
        Vector3I direction = startDir;

        int iterations = _random.RandiRange(1, MaximumIterations);
        int lastIteration = iterations - 1;

        originPos += direction; // Shift one away from the door
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
                    Vector3I newPos = originPos + (perpDir * x) + (direction * z);

                    SetCellItem(newPos, (int)_roomManager.FloorId);
                    floorPosS.Add(newPos);
                }
            }

            if (i == lastIteration) { break; } // Last iteration doesn't need a new direction

            // Choose New Random Direction //
            List<Vector3I> potentialDirs = new(_orthogonalDirs);
            potentialDirs.Remove(-direction); // Remove opposite direction

            Vector3I newDirection = potentialDirs[_random.RandiRange(0, potentialDirs.Count - 1)];

            // Move The Origin To A New Edge //
            // (length - 2) & (outerWidth - 1) = Move one into existing floor, (outerWidth + 1) = Move one out of existing floor
            // This ensures there's a minimum of a 3 cell width connected directly to previous extrusion, so a corridor can be made.
            if (newDirection.Equals(direction))     { originPos += (direction * length) + (perpDir * _random.RandiRange(-(outerWidth - 1), outerWidth - 1)); }
            else if (newDirection.Equals(perpDir))  { originPos += (direction * _random.RandiRange(1, length - 2)) + (perpDir * (outerWidth + 1)); }
            else if (newDirection.Equals(-perpDir)) { originPos += (direction * _random.RandiRange(1, length - 2)) + (-perpDir * (outerWidth + 1)); }
            //

            direction = newDirection;
        }       
        return floorPosS;
    }

    /// <returns><c>List</c> of potential door positions.</returns>
    private List<Vector3I> GenerateWallsAndCeiling(HashSet<Vector3I> floorPosS, int height)
    {
        List<Vector3I> potentialDoorPosS = new();
        IEnumerator<Vector3I> enumerator = floorPosS.GetEnumerator();

        while (enumerator.MoveNext())
        {
            Vector3I floorPos = enumerator.Current;
            NeighbourInfo[] orthNeighbours = GetNeighbours(floorPos, _orthogonalDirs);

            if // Any exposed cell requires a wall
            (
                NeighbourInfo.GetFirstEmpty(orthNeighbours, out NeighbourInfo emptyNeighbour) ||
                NeighbourInfo.GetFirstEmpty(GetNeighbours(floorPos, _diagonalDirs), out _)
            )
            {
                BuildColumn(floorPos, height, _roomManager.WallId);
                floorPosS.Remove(floorPos);

                if // Excludes corners & not having minimum area as potential doorways
                (
                    NeighbourInfo.GetEmptyCount(orthNeighbours) == 1 &&
                    !AreaContainsItems(floorPos + emptyNeighbour.Direction, emptyNeighbour.Direction, MinimumOuterWidth, MinimumLength)
                )
                { potentialDoorPosS.Add(floorPos); }
            }

            // Ceiling
            //SetCellItem(floorPos + (Vector3I.Up * (height)), (int)ItemManager.Id.White);
        }      
        return potentialDoorPosS;
    }

    /// <returns><c>HashSet</c> of connections (doors made by other rooms).</returns>
    private HashSet<Vector3I> MixWallsAndFindConnections(HashSet<Vector3I> floorPosS, Vector3I originDoorAheadPos, int height)
    {
        HashSet<Vector3I> connectionPosS = new();
        IEnumerator<Vector3I> enumerator = floorPosS.GetEnumerator();

        while (enumerator.MoveNext())
        {
            Vector3I floorPos = enumerator.Current;
            NeighbourInfo[] upperOrthNeighbours = GetNeighbours(floorPos + Vector3I.Up, _orthogonalDirs);

            foreach (NeighbourInfo upperNeighbour in upperOrthNeighbours)
            {
                Vector3I neighbourFloorPos = upperNeighbour.Position + Vector3I.Down;
                Vector3I neighbourFloorAheadPos = neighbourFloorPos + upperNeighbour.Direction;

                if (upperNeighbour.Empty)
                {
                    // Check for doors from other rooms (Connections)
                    if (!floorPosS.Contains(neighbourFloorPos))
                    { 
                        connectionPosS.Add(neighbourFloorPos);

                        // Build Column For Door Connection //
                        Vector3I up2 = Vector3I.Up * 2;
                        Vector3I upperPlusUp2 = upperNeighbour.Position + up2;

                        (ItemManager.Id mixedId, int orientation) = GetMixedIdWithOrientation
                        (
                            id1               : _roomManager.WallId,
                            id2               : (ItemManager.Id)GetCellItem(upperPlusUp2),
                            direction         : upperNeighbour.Direction,
                            defaultOrientation: GetCellItemOrientation(upperPlusUp2)
                        );
                        BuildColumn
                        (
                            neighbourFloorPos + up2,
                            height - 2,
                            mixedId,
                            orientation
                        );
                    }
                }
                else if 
                (
                    !floorPosS.Contains(neighbourFloorAheadPos) &&                                    // Different room
                    GetCellItem(neighbourFloorAheadPos)               != (int)ItemManager.Id.Empty && // Floor on other side
                    GetCellItem(neighbourFloorAheadPos + Vector3I.Up) == (int)ItemManager.Id.Empty    // Corners excluded
                )
                {
                    (ItemManager.Id mixedId, int orientation) = GetMixedIdWithOrientation
                    (
                        id1               : _roomManager.WallId,
                        id2               : upperNeighbour.ItemId,
                        direction         : upperNeighbour.Direction,
                        defaultOrientation: GetCellItemOrientation(upperNeighbour.Position)
                    );
                    BuildColumn
                    (
                        neighbourFloorPos,
                        height,
                        mixedId,
                        orientation
                    );
                }
                else { BuildColumn(neighbourFloorPos, height, _roomManager.WallId); } // In case of intrusions from other rooms
            }
        }
        connectionPosS.Remove(originDoorAheadPos);
        return connectionPosS;
    }
    /// <summary>
    /// Gets the mixed ID with its orientation, based on the direction perpendicular to the colour split. Will use <paramref name="defaultOrientation"/> if finding mixed ID fails.<para/>
    /// <paramref name="id1"/> --<paramref name="direction"/>--> <paramref name="id2"/>
    /// </summary>
    /// <returns><c>(ItemManager.Id mixedId, int orientation)</c>.</returns>
    private (ItemManager.Id, int) GetMixedIdWithOrientation(ItemManager.Id id1, ItemManager.Id id2, Vector3I direction, int defaultOrientation = 0)
    {
        int orientation = defaultOrientation;
        if (_itemManager.GetMixedId(id1, id2, out ItemManager.Id mixedId, out bool reversed))
        {
            orientation = GetIndexOfRotationAroundY
            (
                Vector3.Back,
                direction,
                Mathf.Pi * (reversed ? 0.5f : -0.5f)
            );
        }
        return (mixedId, orientation);
    }

    /// <returns><c>HashSet</c> of door positions.</returns>
    private HashSet<Vector3I> GenerateDoors(List<Vector3I> potentialDoorPosS)
    {
        HashSet<Vector3I> doorPosS = new();
        int doorCountTarget = _random.RandiRange(1, MaximumDoorways);

        for (int i = 0; i < doorCountTarget; i++)
        {
            if (potentialDoorPosS.Count == 0) { break; }

            // Create Random Doorway //
            Vector3I doorPos = potentialDoorPosS[_random.RandiRange(0, potentialDoorPosS.Count - 1)];

            BuildColumn(doorPos, 2, ItemManager.Id.Empty);
            doorPosS.Add(doorPos);
            potentialDoorPosS.Remove(doorPos);

            // Remove Orthogonal Potential Doorway Positions (doors shouldn't be created right next to each other) //
            NeighbourInfo[] orthNeighbours = GetNeighbours(doorPos, _orthogonalDirs);
            foreach (NeighbourInfo neighbour in orthNeighbours)
            {
                if (!neighbour.Empty) { potentialDoorPosS.Remove(neighbour.Position); }
            }
        }
        return doorPosS;
    }

    private void GenerateInterior(HashSet<Vector3I> floorPosS, HashSet<Vector3I> doorPosS, Vector3I originDoorAheadPos)
    {       
        int[][] allCellProximities = new int[floorPosS.Count][];
        float[] normaliseVals = { 2f, 2f, 2f, 2f };

        AStar3D aStar = new();
        if (floorPosS.Count > aStar.GetPointCapacity()) { aStar.ReserveSpace(floorPosS.Count); }

        Dictionary<Vector3I, long> posToId = new(floorPosS.Count);
        Dictionary<long, Vector3I> idToPos = new(floorPosS.Count);
        long uniqueId = 0;

        SetProximitiesAndAStar(floorPosS, allCellProximities, normaliseVals, aStar, posToId, idToPos, ref uniqueId);
        HashSet<Vector3I> occupiedPosS = GetAStarPathPositionsBtwDoors(floorPosS, doorPosS, originDoorAheadPos, aStar, posToId, idToPos, ref uniqueId);

        // Attempt To Place Interior Nodes //
        int i = -1;
        foreach (Vector3I floorPos in floorPosS)
        {
            i++;

            if (occupiedPosS.Contains(floorPos)) { continue; }
            int[] cellProximities = allCellProximities[i];

            bool leftEmpty    = cellProximities[(int)AllDir.Left]    > 0,
                 forwardEmpty = cellProximities[(int)AllDir.Forward] > 0,
                 rightEmpty   = cellProximities[(int)AllDir.Right]   > 0,
                 backEmpty    = cellProximities[(int)AllDir.Back]    > 0,
                 nwFilled     = cellProximities[(int)AllDir.NW]     == 0,
                 neFilled     = cellProximities[(int)AllDir.NE]     == 0,
                 seFilled     = cellProximities[(int)AllDir.SE]     == 0,
                 swFilled     = cellProximities[(int)AllDir.SW]     == 0;

            // Invalid Placements //
            if
            (   // Checks For 1 Width Corridor //
                ( (!leftEmpty    && !rightEmpty) && (forwardEmpty && backEmpty) )  || // EnclosedX & OpenZ
                ( (!forwardEmpty && !backEmpty)  && (leftEmpty    && rightEmpty) ) || // EnclosedZ & OpenX

                // Checks In Front For 2 Walls And Middle Gap, In Each Orientation //
                (leftEmpty    && swFilled && nwFilled) || // CW Rotation 0
                (forwardEmpty && nwFilled && neFilled) || // CW Rotation 90
                (rightEmpty   && neFilled && seFilled) || // CW Rotation 180
                (backEmpty    && seFilled && swFilled)    // CW Rotation 270
            )
            { continue; }

            // Place Random Node Based On Minimum Normalised Proximity //
            (float minNormalisedProx, int minIdx) = GetMinimumNormalisedProximityWithIndex(floorPos, cellProximities, normaliseVals, occupiedPosS);
            PlaceRandomInteriorNode(floorPos, floorPosS, occupiedPosS, minNormalisedProx, GetRotationYFromIndex(minIdx));
        }
    }
    private void SetProximitiesAndAStar(HashSet<Vector3I> floorPosS, int[][] allCellProximities, float[] normaliseVals, AStar3D aStar, Dictionary<Vector3I, long> posToId, Dictionary<long, Vector3I> idToPos, ref long uniqueId)
    {
        int i = -1;
        foreach (Vector3I floorPos in floorPosS)
        {
            i++;

            // Find The Directional Proximities //
            allCellProximities[i] = new int[_allDirs.Length];
            Vector3I upperPos = floorPos + Vector3I.Up;

            for (int j = 0; j < _allDirs.Length; j++)
            {
                Vector3I dir = _allDirs[j];
                Vector3I move = dir;
                int dist = 0;

                // Is next cell empty and within room bounds
                while (GetCellItem(upperPos + move) == (int)ItemManager.Id.Empty && floorPosS.Contains(floorPos + move))
                {
                    move = (++dist + 1) * dir;
                }
                allCellProximities[i][j] = dist;
            }
            // Retrieve every other cell proximity (only need max in one direction for each axis)
            for (int j = 0; j < normaliseVals.Length; j++) { normaliseVals[j] = Mathf.Max(normaliseVals[j], allCellProximities[i][j * 2]); }

            // Setup AStar Nodes & Connections //
            long floorPosId = GetAStarId(posToId, idToPos, floorPos, ref uniqueId);

            aStar.AddPoint(floorPosId, floorPos);
            foreach (Vector3I dir in _orthogonalDirs)
            {
                Vector3I pos = floorPos + dir;
                if (floorPosS.Contains(pos))
                {
                    long posId = GetAStarId(posToId, idToPos, pos, ref uniqueId);

                    aStar.AddPoint(posId, pos);
                    aStar.ConnectPoints(floorPosId, posId);
                }
            }
        }
        // Create Normalise Values For Each Axis //
        for (i = 0; i < normaliseVals.Length; i++) { normaliseVals[i] = 2f / normaliseVals[i]; }
    }
    private HashSet<Vector3I> GetAStarPathPositionsBtwDoors(HashSet<Vector3I> floorPosS, HashSet<Vector3I> doorPosS, Vector3I originDoorAheadPos, AStar3D aStar, Dictionary<Vector3I, long> posToId, Dictionary<long, Vector3I> idToPos, ref long uniqueId)
    {
        HashSet<Vector3I> pathPosS = new(floorPosS.Count);
        long originDoorAheadId = GetAStarId(posToId, idToPos, originDoorAheadPos, ref uniqueId);

        foreach (Vector3I otherDoorPos in doorPosS)
        {
            // Find Position Inside Room, Ahead Of Door //
            Vector3I otherDoorAheadPos = Vector3I.Zero;
            NeighbourInfo[] upperOrthNeighbours = GetNeighbours(otherDoorPos + Vector3I.Up, _orthogonalDirs);

            foreach (NeighbourInfo upperNeighbour in upperOrthNeighbours)
            {
                Vector3I floorAheadPos = upperNeighbour.Position + Vector3I.Down;
                if (upperNeighbour.Empty && floorPosS.Contains(floorAheadPos))
                {
                    otherDoorAheadPos = floorAheadPos;
                    break;
                }
            }

            // Create Path From Origin Door To Other Door //
            long[] path = aStar.GetIdPath
            (
                originDoorAheadId,
                GetAStarId(posToId, idToPos, otherDoorAheadPos, ref uniqueId)
            );
            foreach (long id in path) { pathPosS.Add(idToPos[id]); }

            //if (path.Length == 0) { SetCellItem(otherDoorPos + Vector3I.Up * 5, (int)ItemManager.Id.WhiteBlue); } // Use to debug problems
        }
        return pathPosS;
    }
    private (float, int) GetMinimumNormalisedProximityWithIndex(Vector3I floorPos, int[] cellProximities, float[] normaliseVals, HashSet<Vector3I> occupiedPosS)
    {
        (float, int) minNormalisedProx_index = (cellProximities[0], 0);
        for (int j = 1; j < cellProximities.Length; j++)
        {
            float prox = cellProximities[j];
            int oppositeJ = (j + 2) % 4; // For j <= 3

            if
            (
                prox < minNormalisedProx_index.Item1 ||
                (                                                                                            // Prioritise when prox equal:
                    prox == minNormalisedProx_index.Item1 && j <= 3 &&                                       // Orthogonal direction
                    cellProximities[oppositeJ] > 0 && !occupiedPosS.Contains(floorPos + _allDirs[oppositeJ]) // Against wall with opening
                )
            )
            { minNormalisedProx_index = (prox, j); }
        }
        return (minNormalisedProx_index.Item1 * normaliseVals[minNormalisedProx_index.Item2 / 2], minNormalisedProx_index.Item2);
    }
    private float GetRotationYFromIndex(int idx)
    {
        AllDir dir = (AllDir)idx;
        switch (dir)
        {
            case AllDir.Left:
            case AllDir.SW:
            case AllDir.NW:
                return Mathf.Pi * 0.5f;

            case AllDir.Right:
            case AllDir.SE:
            case AllDir.NE:
                return Mathf.Pi * -0.5f;

            case AllDir.Back:
                return Mathf.Pi;

            default: return 0f;
        }
    }
    private void PlaceRandomInteriorNode(Vector3I floorPos, HashSet<Vector3I> floorPosS,  HashSet<Vector3I> occupiedPosS, float minNormalisedProx, float rotationY)
    {
        for (int j = 0; j < MaximumInteriorRetries; j++)
        {
            InteriorObject obj = _roomManager.GetRandomInteriorObject();
            HashSet<Vector3I> clearancePosS = obj.GetClearancePositions(floorPos, rotationY);

            if
            (
                !clearancePosS.Overlaps(occupiedPosS)                                              && // Not intersecting occupied space
                (clearancePosS.IsProperSubsetOf(floorPosS) || clearancePosS.IsSubsetOf(floorPosS)) && // All inside floor space
                (
                    ( obj.Exact && obj.WeightToCentre == minNormalisedProx && _random.Randf() < obj.Rarity) ||
                    (!obj.Exact && _random.Randf() < obj.Rarity * GetProximityProbability(obj.WeightToCentre, minNormalisedProx))
                )
            )
            {
                CreateInteriorNode(obj.Scene, floorPos + obj.GetOffset(rotationY), rotationY);
                occupiedPosS.Add(floorPos);
                occupiedPosS.UnionWith(clearancePosS);

                break;
            }
        }
    }
    private void CreateInteriorNode(PackedScene scene, Vector3 position, float rotationY)
    {
        Node3D node = scene.Instantiate<Node3D>();
        node.Position = position + Vector3.Up;
        node.Rotation = new Vector3(0f, rotationY, 0f);

        _interiorNodeParent.AddChild(node);
    }

    /// <summary>
    /// Tries to get an existing position's ID, otherwise it creates a new entry with the ID and then increments it.
    /// </summary>
    /// <returns>Associated ID for the given <paramref name="posToCheck"/>.</returns>
    private long GetAStarId(Dictionary<Vector3I, long> posToId, Dictionary<long, Vector3I> idToPos, Vector3I posToCheck, ref long uniqueId)
    {
        if (posToId.TryGetValue(posToCheck, out long id)) { return id; }
        else
        {
            posToId.Add(posToCheck, uniqueId);
            idToPos.Add(uniqueId, posToCheck);

            return uniqueId++;
        }
    }

    /// <summary>
    /// Calculates probability depending on the weighting and normalised proximity from the edge of a room.
    /// </summary>
    /// <param name="weight">Weight towards the centre between 0 and 1 (inclusive).</param>
    /// <param name="normalisedProx">Normalised proximity from the edge to the centre.</param>
    /// <returns>Probability represented as a float between 0 and 1 (inclusive).</returns>
    private float GetProximityProbability(float weight, float normalisedProx) => (weight * normalisedProx) + ((1f - weight) * (1f - normalisedProx));

    /// <summary>
    /// Get neighbouring cells at each given direction.
    /// </summary>
    /// <param name="centrePos">Position to get the neighbours from.</param>
    /// <returns><c>NeighbourInfo[]</c> in the same order as the <paramref name="directions"/>.</returns>
    private NeighbourInfo[] GetNeighbours(Vector3I centrePos, Vector3I[] directions)
    {
        NeighbourInfo[] neighbours = new NeighbourInfo[directions.Length];    
        for (int i = 0; i < neighbours.Length; i++)
        {
            Vector3I dir = directions[i];
            Vector3I pos = centrePos + dir;

            neighbours[i] = new NeighbourInfo(pos, dir, GetCellItem(pos));
        }
        return neighbours;
    }

    /// <returns>Perpendicular vector on the y-plane (90 degrees clockwise).</returns>
    private Vector3I GetPerpendicularVector(Vector3I direction) => new(direction.Z, 0, -direction.X);

    /// <returns>Orthogonal index from rotating around the global y-axis.</returns>
    private int GetIndexOfRotationAroundY(Vector3 from, Vector3I to, float angleOffset = 0f)
    {
        float angle = from.SignedAngleTo
        (
            new Vector3(to.X, to.Y, to.Z),
            Vector3.Up
        )
        + angleOffset;

        return GetOrthogonalIndexFromBasis(new Basis(Vector3.Up, angle));
    }

    /// <summary>
    /// Iterates through an area of the <c>GridMap</c> to check for any non-empty cells.
    /// </summary>
    /// <param name="startPos">Bottom centre of rectangular area, relative to the <paramref name="direction"/>.</param>
    /// <param name="direction">Direction to iterate along the length of.</param>
    /// <param name="outerWidth">Number of cells either side of a centre cell.</param>
    /// <param name="length">Number of cells along the <paramref name="direction"/>.</param>
    /// <returns>If the area of cells contains any items.</returns>
    private bool AreaContainsItems(Vector3I startPos, Vector3I direction, int outerWidth, int length)
    {
        Vector3I perpDir = GetPerpendicularVector(direction);
        for (int z = 0; z < length; z++)
        {
            for (int x = -outerWidth; x <= outerWidth; x++)
            {
                Vector3I pos = startPos + (perpDir * x) + (direction * z);
                if (GetCellItem(pos) != (int)ItemManager.Id.Empty)
                { 
                    return true;
                }
            }
        }
        return false;
    }
    ///
    /// <param name="setToExclude">Set of cells to ignore if they are detected in the area.</param>
    private bool AreaContainsItems(Vector3I startPos, Vector3I direction, int outerWidth, int length, HashSet<Vector3I> setToExclude)
    {
        Vector3I perpDir = GetPerpendicularVector(direction);
        for (int z = 0; z < length; z++)
        {
            for (int x = -outerWidth; x <= outerWidth; x++)
            {
                Vector3I pos = startPos + (perpDir * x) + (direction * z);
                if (GetCellItem(pos) != (int)ItemManager.Id.Empty && !setToExclude.Contains(pos))
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
    /// <param name="floorPos">Column will be built ONE above (global y) this position.</param>
    /// <param name="height">Number of cells from the <paramref name="floorPos"/>.</param>
    /// <param name="id">Item ID to set in each cell.</param>
    /// <param name="orientation">Rotation to apply to each cell, approximated as orthogonal indexes.</param>
    private void BuildColumn(Vector3I floorPos, int height, ItemManager.Id id, int orientation = 0)
    {
        for (int y = 1; y <= height; y++)
        {
            SetCellItem(floorPos + (Vector3I.Up * y), (int)id, orientation);
        }
    }
}
