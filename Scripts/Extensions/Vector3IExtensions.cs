using Godot;
using System;

namespace Scripts.Extensions;

public static class Vector3IExtensions
{
    /// <summary>
    /// Get rotated Vector3I <paramref name="v"/> around the y-axis by <paramref name="rotationY"/>.<para/>
    /// Restricted to 0.5<c>PI</c>, <c>PI</c>, 1.5<c>PI</c>, and their negative counterparts (+CCW, -CW to match Godot).
    /// </summary>
    /// <param name="v">Vector to rotate.</param>
    /// <param name="rotationY">Rotation in radians.</param>
    /// <returns>Rotated Vector3I <paramref name="v"/>.</returns>
    public static Vector3I RotatedY(this Vector3I v, float rotationY)
    {
        switch (rotationY)
        {
            case  Mathf.Pi * 0.5f:
            case -Mathf.Pi * 1.5f:
                return new(v.Z, v.Y, -v.X);

            case  Mathf.Pi:
            case -Mathf.Pi:
                return new(-v.X, v.Y, -v.Z);

            case -Mathf.Pi * 0.5f:
            case  Mathf.Pi * 1.5f:
                return new(-v.Z, v.Y, v.X);

            default: return v;
        }
    }
}
