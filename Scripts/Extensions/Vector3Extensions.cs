using Godot;
using System;

namespace Scripts.Extensions;

public static class Vector3Extensions
{
    private static readonly Vector3 _piVec = Vector3.One * Mathf.Pi;

    /// <returns>Angles in the range [-Pi, Pi). Useful for finding shortest angular velocity.</returns>
    public static Vector3 EnsureAngles(this Vector3 v) => (v + _piVec).PosMod(Mathf.Tau) - _piVec;
}
