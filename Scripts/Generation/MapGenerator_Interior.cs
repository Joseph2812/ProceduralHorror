using Godot;
using System;
using System.Collections.Generic;
using Scripts.Generation.Interior;
using Scripts.Generation.Interior.Extension;

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
    /// <param name="iObj"></param>
    /// <param name="position"></param>
    /// <param name="rotationY"></param>
    /// <returns>Whether creation was successful.</returns>
    public bool TryCreateInteriorNode(InteriorObject iObj, Vector3I position, float rotationY)
    {
        (bool canPlace, HashSet<Vector3I> clearancePosS, HashSet<Vector3I> semiClearancePosS) = iObj.CanBePlaced(position, rotationY, _emptyPosS);
        if (!canPlace) { return false; }

        // Create Node //
        Node3D node = iObj.Scene.Instantiate<Node3D>();
        Node3D childNode = node.GetChild<Node3D>(0);

        _interiorNodeParent.AddChild(node);

        childNode.Position -= _interiorNodeOffset;
        node.Position = position + _interiorNodeOffset;
        node.Rotation = iObj.GetRotationWithOffset(rotationY) * Vector3.Up;

        foreach (Vector3I pos in semiClearancePosS) { _emptyPosS[pos] = true; } // Set new & existing to semi-empty
        foreach (Vector3I pos in clearancePosS)     { _emptyPosS.Remove(pos); } // Remove fully occupied

        _potentialPos_floorIdx_heightLvl_S.RemoveAll(x => clearancePosS.Contains(x.Item1) || semiClearancePosS.Contains(x.Item1));
        //

        return true;
    }

    private void GenerateInterior(HashSet<Vector3I> doorPosS, Vector3I originDoorAheadPos, int height)
    {
        // Get Proximities & Setup AStar //
        int[][] allCellProximities = new int[_floorPosS.Count][];

        AStar3D aStar = new();
        if (_floorPosS.Count > aStar.GetPointCapacity()) { aStar.ReserveSpace(_floorPosS.Count); }

        Dictionary<Vector3I, long> posToId = new(_floorPosS.Count);
        Dictionary<long, Vector3I> idToPos = new(_floorPosS.Count);
        long uniqueId = 0;

        SetProximitiesAndAStar(allCellProximities, aStar, posToId, idToPos, ref uniqueId);
        HashSet<Vector3I> pathPosS = GetAStarPathPositionsBtwDoors(doorPosS, originDoorAheadPos, aStar, posToId, idToPos, ref uniqueId);

        // Get All Empty Positions Projected From The Floor //
        int cellCount = _floorPosS.Count * (height - 1);
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
                        nwEmpty     : cellProximities[(int)All3x3Dir.FL]      > 0,
                        neEmpty     : cellProximities[(int)All3x3Dir.FR]      > 0,
                        seEmpty     : cellProximities[(int)All3x3Dir.BR]      > 0,
                        swEmpty     : cellProximities[(int)All3x3Dir.BL]      > 0
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
            (Vector3I potentialPos, int floorIdx, int heightLvl) = _potentialPos_floorIdx_heightLvl_S[randomIdx];

            // Place Random Node Based On Minimum Normalised Proximity //
            (float minNormalisedProx, int minIdx, int totalDist) = GetMinimumNormalisedProximityWithIndex(potentialPos, allCellProximities[floorIdx]);
            PlaceRandomInteriorNode(potentialPos, heightLvl, maxHeightLvl, minNormalisedProx, GetRotationYFromIndex(minIdx), totalDist);
        }
    }

    private void SetProximitiesAndAStar(int[][] allCellProximities, AStar3D aStar, Dictionary<Vector3I, long> posToId, Dictionary<long, Vector3I> idToPos, ref long uniqueId)
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

    /// <returns>(NormalisedProximity, MinIndex, TotalDistance). TotalDistance = Shortest distance along a direction, excluding the placement cell itself (e.g. LeftProx + RightProx).</returns>
    private (float, int, int) GetMinimumNormalisedProximityWithIndex(Vector3I floorPos, int[] cellProximities)
    {
        (int, int, int) minProx_oppositeProx_minIdx = (cellProximities[0], cellProximities[GetOppositeIndex(0)], 0);
        for (int i = 1; i < cellProximities.Length; i++)
        {
            int prox = cellProximities[i];
            int oppositeI = GetOppositeIndex(i);

            if
            (
                prox < minProx_oppositeProx_minIdx.Item1 ||
                (                                                                                              // Prioritise when proximity is equal:
                    prox == minProx_oppositeProx_minIdx.Item1 && i <= 3 &&                                     // Orthogonal direction
                    cellProximities[oppositeI] > 0 && _emptyPosS.ContainsKey(floorPos + All3x3Dirs[oppositeI]) // Against wall with opening
                )
            )
            { minProx_oppositeProx_minIdx = (prox, cellProximities[oppositeI], i); }
        }

        (int minProx, int oppositeProx, int minIdx) = minProx_oppositeProx_minIdx;
        int totalDist = minProx + oppositeProx;
        return
        (
            (totalDist == 0) ? 0 : minProx / (totalDist * 0.5f),
            minIdx,
            totalDist
        );
    }

    /// <summary>
    /// Find index of opposite direction from the given <see cref="All3x3Dir"/> index.
    /// </summary>
    private int GetOppositeIndex(int i) => (i <= 3) ? (i + 2) % 4 : Mathf.Wrap(i + 2, 4, 8);

    private float GetRotationYFromIndex(int idx)
    {
        All3x3Dir dir = (All3x3Dir)idx;
        switch (dir)
        {
            case All3x3Dir.Left:
            case All3x3Dir.BL:
            case All3x3Dir.FL:
                return Mathf.Pi * 0.5f;

            case All3x3Dir.Right:
            case All3x3Dir.BR:
            case All3x3Dir.FR:
                return Mathf.Pi * -0.5f;

            case All3x3Dir.Back:
                return Mathf.Pi;

            default: return 0f;
        }
    }

    private void PlaceRandomInteriorNode(Vector3I pos, int heightLvl, int maxHeightLvl, float minNormalisedProx, float rotationY, int totalDist)
    {
        InteriorObject iObj = _roomManager.GetRandomInteriorObject();

        // Height Constraint Check //
        bool satisfiedHeightConstraint = false;
        switch (iObj.RelativeTo)
        {
            case InteriorObject.Relative.Floor:
                satisfiedHeightConstraint = heightLvl >= iObj.MinimumHeight && heightLvl <= iObj.MaximumHeight;
                break;

            case InteriorObject.Relative.Middle:
                int middleToHeightLvl = heightLvl - Mathf.RoundToInt(maxHeightLvl * 0.5f);
                satisfiedHeightConstraint = middleToHeightLvl >= iObj.MinimumHeight && middleToHeightLvl <= iObj.MaximumHeight;
                break;

            case InteriorObject.Relative.Ceiling:
                int reverseHeightLvl = maxHeightLvl - (heightLvl - 1);
                satisfiedHeightConstraint = reverseHeightLvl >= iObj.MinimumHeight && reverseHeightLvl <= iObj.MaximumHeight;
                break;
        }

        // All Pre-placement Checks //
        if
        (
            !(
                satisfiedHeightConstraint &&
                (
                    (totalDist < 2)                                         || // Weighting doesn't matter below this value (too thin for a middle to exist)
                    ( iObj.Exact && iObj.WeightToMiddle == minNormalisedProx) ||
                    (!iObj.Exact && Rng.Randf() < GetProximityProbability(iObj.WeightToMiddle, minNormalisedProx))
                ) &&
                TryCreateInteriorNode(iObj, pos, rotationY)
            )
        )
        { return; }

        // Extend With Potential Extensions //
        if (iObj is InteriorObjectExtended extendedIObj)
        {
            extendedIObj.CreateExtensionsRecursively(pos, rotationY);
        }
    }

    /// <summary>
    /// Calculates probability depending on the weighting and normalised proximity from the edge of a room.
    /// </summary>
    /// <param name="weight">Weight towards the middle between 0 and 1 (inclusive).</param>
    /// <param name="normalisedProx">Normalised proximity from the edge to the middle.</param>
    /// <returns>Probability represented as a float between 0 and 1 (inclusive).</returns>
    private float GetProximityProbability(float weight, float normalisedProx)
    {
        float normalisedProxPow2 = normalisedProx * normalisedProx;
        return (weight * normalisedProxPow2) + ( (1f - weight) * (1f - normalisedProxPow2) );
    }
}
