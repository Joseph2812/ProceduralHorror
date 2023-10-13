using Godot;
using System;

namespace Scripts.Extensions;

public static class Vector2IExtensions
{
    /// <summary>
    /// Get rotated Vector2I <paramref name="v"/> around the z-axis by <paramref name="rotationZ"/>.<para/>
    /// Restricted to 0.5<c>PI</c>, <c>PI</c>, 1.5<c>PI</c>, and their negative counterparts (+CCW, -CW to match Godot).
    /// </summary>
    /// <param name="v">Vector to rotate.</param>
    /// <param name="rotationZ">Rotation in radians.</param>
    /// <returns>Rotated Vector3I <paramref name="v"/>.</returns>
    public static Vector2I RotatedZ(this Vector2I v, float rotationZ)
    {
        rotationZ %= Mathf.Tau;
        if (rotationZ < 0f) { rotationZ += Mathf.Tau; }

        if (rotationZ >= Mathf.Pi * 0.25f && rotationZ <= Mathf.Pi * 0.75f) { return new(-v.Y,  v.X); }
        if (rotationZ >  Mathf.Pi * 0.75f && rotationZ <= Mathf.Pi * 1.25f) { return new(-v.X, -v.Y); }
        if (rotationZ >  Mathf.Pi * 1.25f && rotationZ <= Mathf.Pi * 1.75f) { return new( v.Y, -v.X); }

        return v;
    }
}
