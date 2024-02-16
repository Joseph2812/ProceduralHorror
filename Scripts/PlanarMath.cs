using Godot;
using System;

namespace Scripts;

public static class PlanarMath
{
    public static Vector3 GetPointOfIntersectionPlane(Vector3 origin, Vector3 direction, Vector3 pointOnPlane, Vector3 normal)
    {
        float k = pointOnPlane.Dot(normal);
        float t = (k - origin.Dot(normal)) / direction.Dot(normal);

        return origin + (direction * t);
    }
}
