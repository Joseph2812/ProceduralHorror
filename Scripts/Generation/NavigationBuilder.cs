using Godot;
using Scripts.Extensions;
using System;
using System.Collections.Generic;

namespace Scripts.Generation;

public static class NavigationBuilder
{
    public enum AgentSize
    {
        //Small = 0x1,
        Medium = 0x10,
        //Large = 0x100
    }

    public readonly struct AgentDimensions(float radius, int height)
    {
        public readonly float Radius = radius;
        public readonly int Height = height;
    }

    private static readonly Dictionary<AgentSize, AgentDimensions> s_agentDimensions = new()
    {
        //{ AgentSize.Small,  new AgentDimensions(radius: 0.49f, height: 1) },
        { AgentSize.Medium, new AgentDimensions(radius: 0.49f, height: 2) },
        //{ AgentSize.Large,  new AgentDimensions(radius: 0.99f, height: 2) }
    };

    public static NavigationRegion3D[] BuildRoomNavigation(Dictionary<Vector3I, bool> emptyPosS, HashSet<Vector3I> floorPosS, int roomHeight)
    {
        // Find bottom and top rows to start from //
        HashSet<Vector3I> emptyBottomRowPosS = [];
        foreach (Vector3I pos in floorPosS)
        {
            Vector3I raisedPos = pos + Vector3I.Up;
            if (emptyPosS.ContainsKey(raisedPos)) { emptyBottomRowPosS.Add(raisedPos); }
        }

        HashSet<Vector3I> emptyTopRowPosS = [];
        foreach (Vector3I pos in floorPosS)
        {
            Vector3I lastRowPos = pos + ((roomHeight - 1) * Vector3I.Up);

            if (!emptyPosS.ContainsKey(lastRowPos)) { continue; }
            emptyTopRowPosS.Add(lastRowPos + Vector3I.Up);
        }

        // Build NavigationRegion3Ds for floor & ceiling //
        Array sizes = Enum.GetValues(typeof(AgentSize));
        NavigationRegion3D[] regions = new NavigationRegion3D[sizes.Length * 2];

        int count = 0;
        foreach (AgentSize size in sizes)
        {
            AgentDimensions dim = s_agentDimensions[size];

            NavigationRegion3D floorRegion = new() { NavigationMesh = new() };
            BuildFloorPolygons(floorRegion.NavigationMesh, dim, emptyPosS, emptyBottomRowPosS);
            regions[count] = floorRegion;
            count++;

            NavigationRegion3D ceilingRegion = new() { NavigationMesh = new() };
            BuildCeilingPolygons(ceilingRegion.NavigationMesh, dim, emptyPosS, emptyTopRowPosS);
            regions[count] = ceilingRegion;
            count++;
        }

        return regions;
    }

    public static NavigationLink3D CreateNewLink(Vector3I doorPos, Vector3I direction)
    {
        Vector3 midpoint = doorPos + Vector3.Up + MapGenerator.Inst.InteriorNodeOffset;
        NavigationLink3D link = new()
        {
            StartPosition = midpoint - direction,
            EndPosition = midpoint + direction,
            Bidirectional = true
        };

        return link;
    }

    private static void BuildFloorPolygons(NavigationMesh navMesh, AgentDimensions dim, Dictionary<Vector3I, bool> emptyPosS, HashSet<Vector3I> emptyBottomRowPosS)
    {
        HashSet<Vector3I> fitsHeightPosS = FilterHeightFitPositions(emptyBottomRowPosS, Vector3I.Up, emptyPosS, dim.Height);
        BuildNavMesh(navMesh, dim, fitsHeightPosS);
    }

    private static void BuildCeilingPolygons(NavigationMesh navMesh, AgentDimensions dim, Dictionary<Vector3I, bool> emptyPosS, HashSet<Vector3I> emptyTopRowPosS)
    {
        HashSet<Vector3I> fitsHeightPosS = FilterHeightFitPositions(emptyTopRowPosS, Vector3I.Down, emptyPosS, dim.Height);
        BuildNavMesh(navMesh, dim, fitsHeightPosS);
    }

    private static HashSet<Vector3I> FilterHeightFitPositions(HashSet<Vector3I> positions, Vector3I dir, Dictionary<Vector3I, bool> emptyPosS, int agentHeight)
    {
        HashSet<Vector3I> fitsHeightPosS = [];
        foreach (Vector3I pos in positions)
        {
            bool blocked = false;
            for (int y = 1; y < agentHeight; y++)
            {
                if (!emptyPosS.ContainsKey(pos + y * dir))
                {
                    blocked = true;
                    break;
                }
            }
            if (!blocked) { fitsHeightPosS.Add(pos); }
        }
        return fitsHeightPosS;
    }

    /// <summary>
    /// Builds out a NavigationMesh by creating a polygon out of each point
    /// </summary>
    private static void BuildNavMesh(NavigationMesh navMesh, AgentDimensions dim, HashSet<Vector3I> validPosS)
    {
        List<Vector3> vertices = [];

        int baseIdx = 0;
        foreach (Vector3I pos in validPosS)
        {
            if (!TryAddVertices(pos, validPosS, vertices, dim.Radius, out int count)) { continue; }

            int[] cellPolygonIndices = new int[count];
            for (int i = 0; i < count; i++) { cellPolygonIndices[i] = baseIdx + i; }
            
            navMesh.AddPolygon(cellPolygonIndices);
            baseIdx += count;
        }
        navMesh.SetVertices([.. vertices]);
    }

    /// <summary> Creates vertices to fit the agent's radius in the surrounding space at this position.</summary>
    /// <returns> Whether it fits and vertices were successfully added. </returns>
    private static bool TryAddVertices(Vector3I pos, HashSet<Vector3I> validPosS, List<Vector3> vertices, float agentRadius, out int count)
    {
        void ShrinkSide(Vector3I dir, float shrinkBy, ref Vector3 vertex1, ref Vector3 vertex2)
        {
            vertex1 -= shrinkBy * (Vector3)dir;
            vertex2 -= shrinkBy * (Vector3)dir;
        }

        // Shrink corner in and create 2 extra vertices to make an inward L-shape //
        (Vector3, Vector3) GetExtraCornerVertices(ref Vector3 corner, Vector3I dir, float shrinkBy)
        {
            corner -= (Vector3)dir * shrinkBy;
            (Vector3, Vector3) extra = (corner + (dir.X * Vector3.Right) * shrinkBy, corner + (dir.Z * Vector3.Back) * shrinkBy);

            if (dir.X * dir.Z < 0) { extra = (extra.Item2, extra.Item1); }
            return extra;
        }

        /// Returns true if it has added extra vertices //
        bool AddCornerVertices(Vector3 corner, (Vector3, Vector3)? extra)
        {
            if (extra != null)
            {
                vertices.Add(extra.Value.Item1);
                vertices.Add(corner);
                vertices.Add(extra.Value.Item2);
                return true;
            }

            vertices.Add(corner);
            return false;
        }

        count = 0;
        
        // Define the 4 corners and their default positions //
        Vector3 topLeft     = new(pos.X, pos.Y, pos.Z);
        Vector3 topRight    = new(pos.X + 1f, pos.Y, pos.Z);
        Vector3 bottomRight = new(pos.X + 1f, pos.Y, pos.Z + 1f);
        Vector3 bottomLeft  = new(pos.X, pos.Y, pos.Z + 1f);

        // Extra vertices to create an inward L-shape when needed //
        (Vector3, Vector3)? extraTopLeft = null;
        (Vector3, Vector3)? extraTopRight = null;
        (Vector3, Vector3)? extraBottomRight = null;
        (Vector3, Vector3)? extraBottomLeft = null;

        // Explore each direction and shrink vertices inwards accordingly (or skip this position entirely if not enough space)
        foreach (Vector3I dir in MapGenerator.Inst.All3x3Dirs)
        {
            int distance = 1;
            while (validPosS.Contains(pos + dir * distance)) { distance++; }

            if ((validPosS.Contains(pos - dir) ? agentRadius : agentRadius * 2f) >= distance) { return false; }
            if (distance > 1) { continue; }

            if      (dir == Vector3I.Left)    { ShrinkSide(Vector3I.Left, agentRadius, ref topLeft, ref bottomLeft); }
            else if (dir == Vector3I.Right)   { ShrinkSide(Vector3I.Right, agentRadius, ref topRight, ref bottomRight); }
            else if (dir == Vector3I.Forward) { ShrinkSide(Vector3I.Forward, agentRadius, ref topLeft, ref topRight); }
            else if (dir == Vector3I.Back)    { ShrinkSide(Vector3I.Back, agentRadius, ref bottomLeft, ref bottomRight); }
            // Check if it's only invalid in the corner //
            else if (dir == MapGenerator.Inst.DiagonalDirs[(int)MapGenerator.DiagDir.FL])
            {
                if (!validPosS.Contains(pos + Vector3I.Forward) || !validPosS.Contains(pos + Vector3I.Left)) { continue; }
                extraTopLeft = GetExtraCornerVertices(ref topLeft, dir, agentRadius);
            }
            else if (dir == MapGenerator.Inst.DiagonalDirs[(int)MapGenerator.DiagDir.FR])
            {
                if (!validPosS.Contains(pos + Vector3I.Forward) || !validPosS.Contains(pos + Vector3I.Right)) { continue; }
                extraTopRight = GetExtraCornerVertices(ref topRight, dir, agentRadius);
            }
            else if (dir == MapGenerator.Inst.DiagonalDirs[(int)MapGenerator.DiagDir.BR])
            {
                if (!validPosS.Contains(pos + Vector3I.Back) || !validPosS.Contains(pos + Vector3I.Right)) { continue; }
                extraBottomRight = GetExtraCornerVertices(ref bottomRight, dir, agentRadius);
            }
            else if (dir == MapGenerator.Inst.DiagonalDirs[(int)MapGenerator.DiagDir.BL])
            {
                if (!validPosS.Contains(pos + Vector3I.Back) || !validPosS.Contains(pos + Vector3I.Left)) { continue; }
                extraBottomLeft = GetExtraCornerVertices(ref bottomLeft, dir, agentRadius);
            }
        }

        // Add using a strict CLOCKWISE order. //
        // Looking from above (+Y down to -Y): Top-Left -> Top-Right -> Bottom-Right -> Bottom-Left //
        count = AddCornerVertices(topLeft, extraTopLeft)         ? count + 3 : count + 1;
        count = AddCornerVertices(topRight, extraTopRight)       ? count + 3 : count + 1;
        count = AddCornerVertices(bottomRight, extraBottomRight) ? count + 3 : count + 1;
        count = AddCornerVertices(bottomLeft, extraBottomLeft)   ? count + 3 : count + 1;

        return true;
    }
}
