using Godot;
using System;
using System.Collections.Generic;
using Scripts.Generation.Interior;

namespace Scripts.Generation;

public partial class MapGenerator : GridMap
{
    private const string InteriorObjectParentName = "InteriorNodes";
    private const int PlaceAttemptsMultiplier = 3;

    private Dictionary<Vector3I, bool> _emptyPosS; // (Position, IsSemiEmpty=true | IsFullyEmpty=false)
    private List<(Vector3I, int, int)> _potentialPos_floorIdx_heightLvl_S;

    private Node _interiorNodeParent;
    private readonly Vector3 _interiorNodeOffset = new Vector3(0.5f, 0f, 0.5f);

    /// <summary>
    /// Creates a <see cref="Node3D"/> from the <see cref="InteriorObject"/>, if it meets the conditions required.
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="position"></param>
    /// <param name="rotationY"></param>
    /// <param name="clearancePosS"></param>
    /// <returns>Whether creation was successful.</returns>
    public bool TryCreateInteriorNode(InteriorObject obj, Vector3I position, float rotationY)
    {
        (bool canPlace, HashSet<Vector3I> clearancePosS, HashSet<Vector3I> semiClearancePosS) = obj.CanBePlaced(position, rotationY, _emptyPosS);
        if (!canPlace) { return false; }

        // Create Node //
        Node3D node = obj.Scene.Instantiate<Node3D>();
        Node3D childNode = node.GetChild<Node3D>(0);

        _interiorNodeParent.AddChild(node);

        childNode.Position -= _interiorNodeOffset;
        node.Position = position + _interiorNodeOffset;
        node.Rotation = obj.GetRotationWithOffset(rotationY) * Vector3.Up;

        foreach (Vector3I pos in semiClearancePosS) { _emptyPosS[pos] = true; } // Set existing to semi-empty
        foreach (Vector3I pos in clearancePosS)     { _emptyPosS.Remove(pos); } // Remove fully occupied

        _potentialPos_floorIdx_heightLvl_S.RemoveAll(x => clearancePosS.Contains(x.Item1) || semiClearancePosS.Contains(x.Item1));
        //

        return true;
    }

    private void GenerateInterior(HashSet<Vector3I> doorPosS, Vector3I originDoorAheadPos, int height)
    {
        // Get Proximities & Setup AStar //
        int[][] allCellProximities = new int[_floorPosS.Count][];
        float[] normaliseVals = { 2f, 2f, 2f, 2f };

        AStar3D aStar = new();
        if (_floorPosS.Count > aStar.GetPointCapacity()) { aStar.ReserveSpace(_floorPosS.Count); }

        Dictionary<Vector3I, long> posToId = new(_floorPosS.Count);
        Dictionary<long, Vector3I> idToPos = new(_floorPosS.Count);
        long uniqueId = 0;

        SetProximitiesAndAStar(allCellProximities, normaliseVals, aStar, posToId, idToPos, ref uniqueId);
        HashSet<Vector3I> pathPosS = GetAStarPathPositionsBtwDoors(doorPosS, originDoorAheadPos, aStar, posToId, idToPos, ref uniqueId);

        // Get All Empty Positions Projected From The Floor //
        int cellCount = _floorPosS.Count * 3;
        _emptyPosS = new(cellCount);
        _potentialPos_floorIdx_heightLvl_S = new(cellCount);

        for (int heightLvl = 1; heightLvl < height; heightLvl++)
        {
            int floorIdx = -1;
            foreach (Vector3I floorPos in _floorPosS)
            {
                floorIdx++;

                Vector3I elevatedPos = floorPos + (Vector3I.Up * heightLvl);
                int[] cellProximities = allCellProximities[floorIdx];

                if
                (
                    heightLvl < 3 &&
                    !IsPlacementValidWithGridMap
                    (
                        leftEmpty   : cellProximities[(int)All3x3Dir.Left]    > 0,
                        forwardEmpty: cellProximities[(int)All3x3Dir.Forward] > 0,
                        rightEmpty  : cellProximities[(int)All3x3Dir.Right]   > 0,
                        backEmpty   : cellProximities[(int)All3x3Dir.Back]    > 0,
                        nwEmpty     : cellProximities[(int)All3x3Dir.NW]      > 0,
                        neEmpty     : cellProximities[(int)All3x3Dir.NE]      > 0,
                        seEmpty     : cellProximities[(int)All3x3Dir.SE]      > 0,
                        swEmpty     : cellProximities[(int)All3x3Dir.SW]      > 0
                    )
                )
                { continue; }

                _emptyPosS.Add(elevatedPos, pathPosS.Contains(elevatedPos));

                if (Rng.Randf() < _roomManager.SelectedRoom.ChanceOfEmptyCell) { continue; }
                _potentialPos_floorIdx_heightLvl_S.Add((elevatedPos, floorIdx, heightLvl));
            }
        }

        // Attempt To Place Interior Nodes //
        int maxHeightLvl = height - 1;
        for (int i = 0; i < _potentialPos_floorIdx_heightLvl_S.Count * PlaceAttemptsMultiplier; i++) // Attempts Are Proportional To Count
        {
            if (_potentialPos_floorIdx_heightLvl_S.Count == 0) { break; }

            int randomIdx = Rng.RandiRange(0, _potentialPos_floorIdx_heightLvl_S.Count - 1);
            (Vector3I potentialPos, int floorIdx, int heightLvl) =  _potentialPos_floorIdx_heightLvl_S[randomIdx];

            // Place Random Node Based On Minimum Normalised Proximity //
            (float minNormalisedProx, int minIdx) = GetMinimumNormalisedProximityWithIndex(potentialPos, allCellProximities[floorIdx], normaliseVals);
            PlaceRandomInteriorNode(potentialPos, maxHeightLvl, heightLvl, minNormalisedProx, GetRotationYFromIndex(minIdx));
        }
    }

    private void SetProximitiesAndAStar(int[][] allCellProximities, float[] normaliseVals, AStar3D aStar, Dictionary<Vector3I, long> posToId, Dictionary<long, Vector3I> idToPos, ref long uniqueId)
    {
        int i = -1;
        foreach (Vector3I floorPos in _floorPosS)
        {
            i++;

            // Find The Directional Proximities //
            allCellProximities[i] = new int[All3x3Dirs.Length];
            Vector3I upperPos = floorPos + Vector3I.Up;

            for (int j = 0; j < All3x3Dirs.Length; j++)
            {
                Vector3I dir = All3x3Dirs[j];
                Vector3I move = dir;
                int dist = 0;

                // Is next cell empty and within room bounds
                while (GetCellItem(upperPos + move) == (int)ItemManager.Id.Empty && _floorPosS.Contains(floorPos + move))
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
            foreach (Vector3I dir in OrthogonalDirs)
            {
                Vector3I pos = floorPos + dir;
                if (_floorPosS.Contains(pos))
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

    private HashSet<Vector3I> GetAStarPathPositionsBtwDoors(HashSet<Vector3I> doorPosS, Vector3I originDoorAheadPos, AStar3D aStar, Dictionary<Vector3I, long> posToId, Dictionary<long, Vector3I> idToPos, ref long uniqueId)
    {
        HashSet<Vector3I> pathPosS = new(_floorPosS.Count);
        long originDoorAheadId = GetAStarId(posToId, idToPos, originDoorAheadPos, ref uniqueId);

        foreach (Vector3I otherDoorPos in doorPosS)
        {
            // Find Position Inside Room, Ahead Of Door //
            Vector3I otherDoorAheadPos = Vector3I.Zero;
            NeighbourInfo[] upperOrthNeighbours = GetNeighbours(otherDoorPos + Vector3I.Up, OrthogonalDirs);

            foreach (NeighbourInfo upperNeighbour in upperOrthNeighbours)
            {
                Vector3I floorAheadPos = upperNeighbour.Position + Vector3I.Down;
                if (upperNeighbour.Empty && _floorPosS.Contains(floorAheadPos))
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
            foreach (long id in path)
            {
                Vector3I floorPos = idToPos[id];

                // Player Size Above Floor //
                pathPosS.Add(floorPos + Vector3I.Up);
                pathPosS.Add(floorPos + (Vector3I.Up * 2));
            }
        }
        return pathPosS;
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

    private bool IsPlacementValidWithGridMap(bool leftEmpty, bool forwardEmpty, bool rightEmpty, bool backEmpty, bool nwEmpty, bool neEmpty, bool seEmpty, bool swEmpty)
    {
        return
        !(
            // Checks For 1 Width Corridor //
            ( (!leftEmpty    && !rightEmpty) && (forwardEmpty && backEmpty) )  || // EnclosedX & OpenZ
            ( (!forwardEmpty && !backEmpty)  && (leftEmpty    && rightEmpty) ) || // EnclosedZ & OpenX

            // Checks In Front For 2 Walls And Middle Gap, In Each Orientation //
            (leftEmpty    && !swEmpty && !nwEmpty) || // CW Rotation 0
            (forwardEmpty && !nwEmpty && !neEmpty) || // CW Rotation 90
            (rightEmpty   && !neEmpty && !seEmpty) || // CW Rotation 180
            (backEmpty    && !seEmpty && !swEmpty)    // CW Rotation 270
        );
    }

    private (float, int) GetMinimumNormalisedProximityWithIndex(Vector3I floorPos, int[] cellProximities, float[] normaliseVals)
    {
        (float, int) minNormalisedProx_index = (cellProximities[0], 0);
        for (int j = 1; j < cellProximities.Length; j++)
        {
            float prox = cellProximities[j];
            int oppositeJ = (j + 2) % 4; // For j <= 3

            if
            (
                prox < minNormalisedProx_index.Item1 ||
                (                                                                                              // Prioritise when prox equal:
                    prox == minNormalisedProx_index.Item1 && j <= 3 &&                                         // Orthogonal direction
                    cellProximities[oppositeJ] > 0 && _emptyPosS.ContainsKey(floorPos + All3x3Dirs[oppositeJ]) // Against wall with opening
                )
            )
            { minNormalisedProx_index = (prox, j); }
        }
        return (minNormalisedProx_index.Item1 * normaliseVals[minNormalisedProx_index.Item2 / 2], minNormalisedProx_index.Item2);
    }

    private float GetRotationYFromIndex(int idx)
    {
        All3x3Dir dir = (All3x3Dir)idx;
        switch (dir)
        {
            case All3x3Dir.Left:
            case All3x3Dir.SW:
            case All3x3Dir.NW:
                return Mathf.Pi * 0.5f;

            case All3x3Dir.Right:
            case All3x3Dir.SE:
            case All3x3Dir.NE:
                return Mathf.Pi * -0.5f;

            case All3x3Dir.Back:
                return Mathf.Pi;

            default: return 0f;
        }
    }

    private void PlaceRandomInteriorNode(Vector3I pos, int maxHeightLvl, int heightLvl, float minNormalisedProx, float rotationY)
    {
        InteriorObject obj = _roomManager.GetRandomInteriorObject();
        if
        (
            !(
                (
                    ( obj.OnlyCeiling && heightLvl == maxHeightLvl) ||
                    (!obj.OnlyCeiling && heightLvl >= obj.MinimumHeight && heightLvl <= obj.MaximumHeight)
                ) &&
                (
                    ( obj.Exact && obj.WeightToCentre == minNormalisedProx) ||
                    (!obj.Exact && Rng.Randf() < GetProximityProbability(obj.WeightToCentre, minNormalisedProx))
                ) &&
                TryCreateInteriorNode(obj, pos, rotationY)
            )
        )
        { return; }

        // Extend With Potential Extensions //
        if (obj is InteriorObjectExtended extendedObj)
        {
            extendedObj.CreateExtensionsRecursively(pos, rotationY);
        }
    }

    /// <summary>
    /// Calculates probability depending on the weighting and normalised proximity from the edge of a room.
    /// </summary>
    /// <param name="weight">Weight towards the centre between 0 and 1 (inclusive).</param>
    /// <param name="normalisedProx">Normalised proximity from the edge to the centre.</param>
    /// <returns>Probability represented as a float between 0 and 1 (inclusive).</returns>
    private float GetProximityProbability(float weight, float normalisedProx) => (weight * normalisedProx) + ((1f - weight) * (1f - normalisedProx));
}
