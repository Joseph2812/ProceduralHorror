using Godot;
using Scripts.Extensions;
using System.Collections.Generic;

namespace Scripts.Generation;

public static class NavigationBuilder
{
    public static (NavigationRegion3D floorRegion, NavigationRegion3D ceilingRegion) BuildRoomNavigation(Dictionary<Vector3I, bool> emptyPosS, HashSet<Vector3I> floorPosS, int height)
    {
        HashSet<Vector3I> emptyFirstRowPosS = [];
        foreach (Vector3I pos in floorPosS)
        {
            Vector3I raisedPos = pos + Vector3I.Up;
            if (emptyPosS.ContainsKey(raisedPos)) { emptyFirstRowPosS.Add(raisedPos); }
        }

        NavigationRegion3D floorRegion = new() { NavigationMesh = new() };
        BuildFloorPolygons(floorRegion.NavigationMesh, emptyFirstRowPosS);

        NavigationRegion3D ceilingRegion = new() { NavigationMesh = new() };
        BuildCeilingPolygons(ceilingRegion.NavigationMesh, emptyPosS, floorPosS, height);

        return (floorRegion, ceilingRegion);
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

    private static void BuildFloorPolygons(NavigationMesh navMesh, HashSet<Vector3I> emptyFirstRowPosS)
    {
        BuildNavMesh(navMesh, emptyFirstRowPosS, emptyFirstRowPosS.Count);
    }

    private static void BuildCeilingPolygons(NavigationMesh navMesh, Dictionary<Vector3I, bool> emptyPosS, HashSet<Vector3I> floorPosS, int height)
    {
        List<Vector3I> emptylastRowPosS = [];
        foreach (Vector3I pos in floorPosS)
        {
            Vector3I lastRowPos = pos + ((height - 1) * Vector3I.Up);

            if (!emptyPosS.ContainsKey(lastRowPos)) { continue; }
            emptylastRowPosS.Add(lastRowPos + Vector3I.Up);
        }

        BuildNavMesh(navMesh, emptylastRowPosS, emptylastRowPosS.Count);
    }

    /// <summary>
    /// Builds out a NavigationMesh by creating a 4-sided polygon out of each point
    /// </summary>
    private static void BuildNavMesh(NavigationMesh navMesh, IEnumerable<Vector3I> points, int count)
    {
        Vector3[] vertices = new Vector3[count * 4];

        int baseIdx = 0;
        foreach (Vector3I pos in points)
        {
            // Define the 4 corners of THIS polygon relative to its position //
            Vector3 topLeft     = new(pos.X, pos.Y, pos.Z);
            Vector3 topRight    = new(pos.X + 1f, pos.Y, pos.Z);
            Vector3 bottomRight = new(pos.X + 1f, pos.Y, pos.Z + 1f);
            Vector3 bottomLeft  = new(pos.X, pos.Y, pos.Z + 1f);

            vertices[baseIdx] = topLeft;
            vertices[baseIdx + 1] = topRight;
            vertices[baseIdx + 2] = bottomRight;
            vertices[baseIdx + 3] = bottomLeft;

            // Create a polygon for this tile using a strict CLOCKWISE order. //
            // Looking from above (+Y down to -Y): Top-Left -> Top-Right -> Bottom-Right -> Bottom-Left //
            int[] cellPolygonIndices =
            [
                baseIdx, 
                baseIdx + 1, 
                baseIdx + 2, 
                baseIdx + 3 
            ];
            
            navMesh.AddPolygon(cellPolygonIndices);
            baseIdx += 4;
        }
        navMesh.SetVertices(vertices);
    }
}
