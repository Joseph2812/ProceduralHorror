#define ENABLE_CEILING

using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Scripts.Extensions;

namespace Scripts.Generation;

public partial class MapGenerator : GridMap
{  
    private const int MillisecondsBtwSteps = 250; // Slows down generation by adding this delay between steps
    private const int MaximumExtrusionRetries = 50;

    public enum OrthDir
    {
        Left,
        Forward,
        Right,
        Back
    }
    public enum DiagDir
    {
        NW,
        NE,
        SE,
        SW
    }

    /// <summary>
    /// Directions:<br/>
    /// | NW   | Forward | NE    |<br/>
    /// | Left | Centre  | Right |<br/>
    /// | SW   | Back    | SE    |<para/>
    /// </summary>
    public enum All3x3Dir
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

    /// <summary>
    /// <inheritdoc cref="All3x3Dir"/>
    /// 
    /// Relative to cell's y:<br/>
    /// 0) y - 1<br/>
    /// 1) y<br/>
    /// 2) y + 1
    /// </summary>
    public enum All3x3x3Dir
    {
        Left0,
        Forward0,
        Right0,
        Back0,
        NW0,
        NE0,
        SE0,
        SW0,

        Left1,
        Forward1,
        Right1,
        Back1,
        NW1,
        NE1,
        SE1,
        SW1,

        Left2,
        Forward2,
        Right2,
        Back2,
        NW2,
        NE2,
        SE2,
        SW2
    }

    public static MapGenerator Inst { get; private set; }

    public RandomNumberGenerator Rng { get; private set; } = new();

    // Directions //
    public Vector3I[] OrthogonalDirs { get; private set; } =
    {
        Vector3I.Left,
        Vector3I.Forward,
        Vector3I.Right,
        Vector3I.Back
    };
    public Vector3I[] DiagonalDirs { get; private set; } =
    {
        new Vector3I(-1, 0, -1), // North West (North = -Z)
        new Vector3I(1, 0, -1),  // North East
        new Vector3I(1, 0, 1),   // South East
        new Vector3I(-1, 0, 1)   // South West
    };
    public Vector3I[] All3x3Dirs { get; private set; } // Orthogonal + Diagonal
    public Vector3I[] All3x3x3Dirs { get; private set; } // _all3x3Dirs x3 along y
    //

    private ItemManager _itemManager = new();
    private RoomManager _roomManager = new();
    private HashSet<Vector3I> _floorPosS;

    public MapGenerator() { Inst = this; }

    public override async void _Ready()
    {
        base._Ready();

        // Initialise //
        MeshLibrary = _itemManager.GetMeshLibrary();
        _interiorNodeParent = new Node3D { Name = InteriorObjectParentName };
        AddChild(_interiorNodeParent);

        // Setting Directions //
        int orthDiagLength = OrthogonalDirs.Length + DiagonalDirs.Length;

        All3x3Dirs = new Vector3I[orthDiagLength];
        OrthogonalDirs.CopyTo(All3x3Dirs, 0);
        DiagonalDirs.CopyTo(All3x3Dirs, OrthogonalDirs.Length);

        All3x3x3Dirs = new Vector3I[orthDiagLength * 3];
        for (int i = 0; i < All3x3Dirs.Length; i++) { All3x3x3Dirs[i]                           = All3x3Dirs[i] + Vector3I.Down; }
        for (int i = 0; i < All3x3Dirs.Length; i++) { All3x3x3Dirs[i + All3x3Dirs.Length]       = All3x3Dirs[i]; }
        for (int i = 0; i < All3x3Dirs.Length; i++) { All3x3x3Dirs[i + (All3x3Dirs.Length * 2)] = All3x3Dirs[i] + Vector3I.Up; }
        //

        Rng.Seed = 184690118043452219;
        GD.Print(Rng.Seed);

        bool success = false;
        while (!success) { success = await StartGeneration(); }

        // Free Unused Objects //
        Rng.Dispose();

        OrthogonalDirs = null;
        DiagonalDirs = null;
        All3x3Dirs = null;
        All3x3x3Dirs = null;

        _itemManager = null;
        _roomManager = null;

        _floorPosS = null;
        _emptyPosS = null;
        _potentialPos_floorIdx_heightLvl_S = null;
    }

    /// <summary>
    /// Get neighbouring cells on the <see cref="GridMap"/> at each given direction.
    /// </summary>
    /// <param name="centrePos">Position to get the neighbours from.</param>
    /// <returns><see cref="NeighbourInfo"/>[] in the same order as the <paramref name="directions"/>.</returns>
    public NeighbourInfo[] GetNeighbours(Vector3I centrePos, Vector3I[] directions)
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

    private async Task<bool> StartGeneration()
    {
        HashSet<Vector3I> prevDoorPosS = new() { Vector3I.Zero };
        HashSet<Vector3I> allDoorPosS = new();
        int roomCount = 0;

        while (prevDoorPosS.Count > 0)
        {
            HashSet<Vector3I> doorPosS = new();
            foreach (Vector3I doorPos in prevDoorPosS)
            {
                if (roomCount == _roomManager.MaximumRoomCount)
                {
                    doorPosS.Clear();
                    break;
                }
                _roomManager.SelectRandomRoom();

                _floorPosS = GenerateFloor(doorPos, out Vector3I startDir);
                await Task.Delay(MillisecondsBtwSteps);

                if (_floorPosS == null) { continue; }

                Vector3I originDoorAheadPos = (doorPos == Vector3I.Zero) ? doorPos + (startDir * 2) : doorPos + startDir;
                int height = Rng.RandiRange(_roomManager.SelectedRoom.MinimumHeight, _roomManager.SelectedRoom.MaximumHeight);

                List<Vector3I> potentialDoorPosS = GenerateWallsAndCeiling((doorPos == Vector3I.Zero) ? doorPos + startDir : doorPos, height);
                await Task.Delay(MillisecondsBtwSteps);
                HashSet<Vector3I> connectionPosS = MixWallsAndFindConnections(originDoorAheadPos, height);
                await Task.Delay(MillisecondsBtwSteps);
                HashSet<Vector3I> newDoorPosS = GenerateDoors(potentialDoorPosS);
                await Task.Delay(MillisecondsBtwSteps);

                allDoorPosS.UnionWith(newDoorPosS);
                doorPosS.UnionWith(newDoorPosS);

                newDoorPosS.UnionWith(connectionPosS); // Add doors from other rooms (Connections)
                GenerateInterior(newDoorPosS, originDoorAheadPos, height);
                await Task.Delay(MillisecondsBtwSteps);

                roomCount++;
            }
            prevDoorPosS = doorPosS;
        }

        if (roomCount < _roomManager.MaximumRoomCount)
        {
            GD.Print($"Maximum room count wasn't reached. Current: {roomCount}. Retrying...");

            Clear();
            _interiorNodeParent.Free();
            _interiorNodeParent = new Node3D { Name = InteriorObjectParentName };
            AddChild(_interiorNodeParent);

            return false;
        }

        // Fill Any Doors Open To The VOID Or Leading Into A Wall //
        foreach (Vector3I pos in allDoorPosS)
        {
            Vector3I upperPos = pos + Vector3I.Up;
            if
            (               
                NeighbourInfo.GetFirstEmpty(GetNeighbours(pos, OrthogonalDirs), out _) ||
                NeighbourInfo.GetFirstEmpty(GetNeighbours(pos, DiagonalDirs), out _) ||
                // Check if it's leading into a wall
                ( 
                    NeighbourInfo.GetFirstEmpty(GetNeighbours(upperPos, OrthogonalDirs), out NeighbourInfo emptyNeighbour) &&
                    GetCellItem(upperPos - emptyNeighbour.Direction) != (int)ItemManager.Id.Empty
                )
            )
            {
                Vector3I aboveDoor = pos + (Vector3I.Up * 3);
                BuildColumn(pos, 2, (ItemManager.Id)GetCellItem(aboveDoor), GetCellItemOrientation(aboveDoor));
            }
        }
        return true;
    }

    /// <returns><see cref="HashSet{Vector3I}"/> of floor positions.</returns>
    private HashSet<Vector3I> GenerateFloor(Vector3I originPos, out Vector3I startDir)
    {
        // Get Initial Direction //
        if (!NeighbourInfo.GetFirstEmpty(GetNeighbours(originPos, OrthogonalDirs), out NeighbourInfo emptyNeighbour))
        {
            startDir = Vector3I.Zero;
            return null;
        }
        startDir = emptyNeighbour.Direction;

        // Extrude A Random Number Of Times //
        HashSet<Vector3I> _currentFloorPosS = new(); // For walls later
        Vector3I direction = startDir;

        int iterations = Rng.RandiRange(_roomManager.SelectedRoom.MinimumExtrusionIterations, _roomManager.SelectedRoom.MaximumExtrusionIterations);
        int lastIteration = iterations - 1;

        originPos += direction; // Shift one away from the door
        for (int i = 0; i < iterations; i++)
        {
            Vector3I perpDir = direction.RotatedY(Mathf.Pi * -0.5f);
            int outerWidth = 0;
            int length = 0;

            // Keep Attempting Random Extrusions //
            bool extrusionFits = false;
            for (int j = 0; j < MaximumExtrusionRetries; j++)
            {
                outerWidth = Rng.RandiRange(_roomManager.SelectedRoom.MinimumOuterWidth, _roomManager.SelectedRoom.MaximumOuterWidth);
                length = Rng.RandiRange(_roomManager.SelectedRoom.MinimumLength, _roomManager.SelectedRoom.MaximumLength);

                // Check If The Area Is Clear //
                if (!AreaContainsItems(originPos, direction, outerWidth, length, _currentFloorPosS))
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

                    SetCellItem(newPos, (int)_roomManager.SelectedRoom.FloorId);
                    _currentFloorPosS.Add(newPos);
                }
            }

            if (i == lastIteration) { break; } // Last iteration doesn't need a new direction

            // Choose New Random Direction //
            List<Vector3I> potentialDirs = new(OrthogonalDirs);
            potentialDirs.Remove(-direction); // Remove opposite direction

            Vector3I newDirection = potentialDirs[Rng.RandiRange(0, potentialDirs.Count - 1)];

            // Move The Origin To A New Edge //
            // (length - 2) & (outerWidth - 1) = Move one into existing floor, (outerWidth + 1) = Move one out of existing floor
            // This ensures there's a minimum of a 3 cell width connected directly to previous extrusion, so a corridor can be made
            if (newDirection.Equals(direction))     { originPos += (direction * length) + (perpDir * Rng.RandiRange(-(outerWidth - 1), outerWidth - 1)); }
            else if (newDirection.Equals(perpDir))  { originPos += (direction * Rng.RandiRange(1, length - 2)) + (perpDir * (outerWidth + 1)); }
            else if (newDirection.Equals(-perpDir)) { originPos += (direction * Rng.RandiRange(1, length - 2)) + (-perpDir * (outerWidth + 1)); }
            //

            direction = newDirection;
        }       
        return _currentFloorPosS;
    }

    /// <returns><see cref="List{Vector3I}"/> of potential door positions.</returns>
    private List<Vector3I> GenerateWallsAndCeiling(Vector3I doorPos, int height)
    {
        List<Vector3I> potentialDoorPosS = new();
        IEnumerator<Vector3I> enumerator = _floorPosS.GetEnumerator();

        while (enumerator.MoveNext())
        {
            Vector3I floorPos = enumerator.Current;
            NeighbourInfo[] orthNeighbours = GetNeighbours(floorPos, OrthogonalDirs);

            if // Any exposed cell requires a wall
            (
                NeighbourInfo.GetFirstEmpty(orthNeighbours, out NeighbourInfo emptyNeighbour) ||
                NeighbourInfo.GetFirstEmpty(GetNeighbours(floorPos, DiagonalDirs), out _)
            )
            {
                BuildColumn(floorPos, height, _roomManager.SelectedRoom.WallId);
                _floorPosS.Remove(floorPos);

                if // Excludes corners & not having minimum area as potential doorways
                (
                    NeighbourInfo.GetEmptyCount(orthNeighbours) == 1 &&
                    !AreaContainsItems(floorPos + emptyNeighbour.Direction, emptyNeighbour.Direction, _roomManager.SelectedRoom.MinimumOuterWidth, _roomManager.SelectedRoom.MinimumLength)
                )
                { potentialDoorPosS.Add(floorPos); }
            }

#if ENABLE_CEILING
            SetCellItem(floorPos + (Vector3I.Up * height), (int)_roomManager.SelectedRoom.CeilingId);
#endif
        }
#if ENABLE_CEILING
        SetCellItem(doorPos + (Vector3I.Up * height), (int)_roomManager.SelectedRoom.CeilingId);
#endif
        return potentialDoorPosS;
    }

    /// <returns><see cref="HashSet{Vector3I}"/> of connections (doors made by other rooms).</returns>
    private HashSet<Vector3I> MixWallsAndFindConnections(Vector3I originDoorAheadPos, int height)
    {
        HashSet<Vector3I> connectionPosS = new();
        IEnumerator<Vector3I> enumerator = _floorPosS.GetEnumerator();

        while (enumerator.MoveNext())
        {
            Vector3I floorPos = enumerator.Current;
            NeighbourInfo[] orthUpperNeighbours = GetNeighbours(floorPos + Vector3I.Up, OrthogonalDirs);

            foreach (NeighbourInfo orthNeighbour in orthUpperNeighbours)
            {
                Vector3I neighbourFloorPos = orthNeighbour.Position + Vector3I.Down;
                Vector3I neighbourFloorAheadPos = neighbourFloorPos + orthNeighbour.Direction;

                if (orthNeighbour.Empty)
                {
                    // Check for doors from other rooms (Connections)
                    if (!_floorPosS.Contains(neighbourFloorPos))
                    { 
                        connectionPosS.Add(neighbourFloorPos);

                        // Build Column For Door Connection //
                        Vector3I orthNeighbourPlusPerpDir = orthNeighbour.Position + orthNeighbour.Direction.RotatedY(Mathf.Pi * -0.5f);

                        (ItemManager.Id mixedId, int orientation) = GetMixedIdWithOrientation
                        (
                            id1               : _roomManager.SelectedRoom.WallId,
                            id2               : (ItemManager.Id)GetCellItem(orthNeighbourPlusPerpDir),
                            direction         : orthNeighbour.Direction,
                            defaultOrientation: GetCellItemOrientation(orthNeighbourPlusPerpDir)
                        );
                        BuildColumn
                        (
                            neighbourFloorPos + (Vector3I.Up * 2),
                            height - 2,
                            mixedId,
                            orientation
                        );
                    }
                }
                else if 
                (
                    !_floorPosS.Contains(neighbourFloorAheadPos)                                   && // Different room
                    GetCellItem(neighbourFloorAheadPos)               != (int)ItemManager.Id.Empty && // Floor on other side
                    GetCellItem(neighbourFloorAheadPos + Vector3I.Up) == (int)ItemManager.Id.Empty    // Corners excluded
                )
                {
                    (ItemManager.Id mixedId, int orientation) = GetMixedIdWithOrientation
                    (
                        id1               : _roomManager.SelectedRoom.WallId,
                        id2               : orthNeighbour.ItemId,
                        direction         : orthNeighbour.Direction,
                        defaultOrientation: GetCellItemOrientation(orthNeighbour.Position)
                    );
                    BuildColumn
                    (
                        neighbourFloorPos,
                        height,
                        mixedId,
                        orientation
                    );
                }
                else { BuildColumn(neighbourFloorPos, height, _roomManager.SelectedRoom.WallId); } // In case of intrusions from other rooms
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

    /// <returns><see cref="HashSet{Vector3I}"/> of door positions.</returns>
    private HashSet<Vector3I> GenerateDoors(List<Vector3I> potentialDoorPosS)
    {
        HashSet<Vector3I> doorPosS = new();
        int doorCountTarget = Rng.RandiRange(_roomManager.SelectedRoom.MinimumDoorways, _roomManager.SelectedRoom.MaximumDoorways);

        for (int i = 0; i < doorCountTarget; i++)
        {
            if (potentialDoorPosS.Count == 0) { break; }

            // Create Random Doorway //
            Vector3I doorPos = potentialDoorPosS[Rng.RandiRange(0, potentialDoorPosS.Count - 1)];

            BuildColumn(doorPos, 2, ItemManager.Id.Empty);
            doorPosS.Add(doorPos);
            potentialDoorPosS.Remove(doorPos);

            // Remove Orthogonal Potential Doorway Positions (doors shouldn't be created right next to each other) //
            NeighbourInfo[] orthNeighbours = GetNeighbours(doorPos, OrthogonalDirs);
            foreach (NeighbourInfo neighbour in orthNeighbours)
            {
                if (!neighbour.Empty) { potentialDoorPosS.Remove(neighbour.Position); }
            }
        }
        return doorPosS;
    }

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
    /// Iterates through an area of the <see cref="GridMap"/> to check for any non-empty cells.
    /// </summary>
    /// <param name="startPos">Bottom centre of rectangular area, relative to the <paramref name="direction"/>.</param>
    /// <param name="direction">Direction to iterate along the length of.</param>
    /// <param name="outerWidth">Number of cells either side of a centre cell.</param>
    /// <param name="length">Number of cells along the <paramref name="direction"/>.</param>
    /// <returns>If the area of cells contains any items.</returns>
    private bool AreaContainsItems(Vector3I startPos, Vector3I direction, int outerWidth, int length)
    {
        Vector3I perpDir = direction.RotatedY(Mathf.Pi * -0.5f);
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
    /// <inheritdoc cref="AreaContainsItems(Vector3I, Vector3I, int, int)"/>
    /// <param name="setToExclude">Set of cells to ignore if they are detected in the area.</param>
    private bool AreaContainsItems(Vector3I startPos, Vector3I direction, int outerWidth, int length, HashSet<Vector3I> setToExclude)
    {
        Vector3I perpDir = direction.RotatedY(Mathf.Pi * -0.5f);
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
