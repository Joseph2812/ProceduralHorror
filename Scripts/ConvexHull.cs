using Godot;
using System;
using System.Drawing;

namespace Scripts;

public static class ConvexHull
{
    public static ConvexPolygonShape3D GeneratePolygon(Vector3[] points)
    {
        throw new NotImplementedException();
    }
    
    /// <returns>(Box, Offset)</returns>
    public static (BoxShape3D, Vector3) GenerateBox(params Vector3[][] pointGroups)
    {
        (Vector3 size, Vector3 offset) = GetBounds(pointGroups);
        return
        (
            new() { Size = size },
            offset
        );
    }

    /// <returns>(Capsule, Offset)</returns>
    public static (CapsuleShape3D, Vector3) GenerateCapsule(params Vector3[][] pointGroups)
    {
        (Vector3 size, Vector3 offset) = GetBounds(pointGroups);
        return
        (
            new() { Radius = Mathf.Max(size.X, size.Y) * 0.5f, Height = size.Z },
            offset
        );
    }

    /// <returns>(Size, Offset)</returns>
    private static (Vector3, Vector3) GetBounds(params Vector3[][] pointGroups)
    {
        Vector3 firstP = pointGroups[0][0];
        float lowestX = firstP.X, highestX = firstP.X;
        float lowestY = firstP.Y, highestY = firstP.Y;
        float lowestZ = firstP.Z, highestZ = firstP.Z;

        foreach (Vector3[] points in pointGroups)
        {
            foreach (Vector3 p in points)
            {
                if      (p.X < lowestX)  { lowestX  = p.X; }
                else if (p.X > highestX) { highestX = p.X; }

                if      (p.Y < lowestY)  { lowestY  = p.Y; }
                else if (p.Y > highestY) { highestY = p.Y; }

                if      (p.Z < lowestZ)  { lowestZ  = p.Z; }
                else if (p.Z > highestZ) { highestZ = p.Z; }
            }
        }
        Vector3 size = new
        (
            highestX - lowestX,
            highestY - lowestY,
            highestZ - lowestZ
        );

        return new
        (
            size,
            (size * 0.5f) + new Vector3(lowestX, lowestY, lowestZ)
        );
    }
}
