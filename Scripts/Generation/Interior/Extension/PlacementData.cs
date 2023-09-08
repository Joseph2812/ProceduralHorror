using Godot;
using System;

namespace Scripts.Generation.Interior.Extension;

[GlobalClass]
[Tool]
public partial class PlacementData : Resource
{
    /// <summary>
    /// Where should all the <see cref="InteriorObject"/>s could be placed relative to the root <see cref="InteriorObject"/>.
    /// </summary>
    [Export]
    public Vector3I Position { get; set; }

    /// <summary>
    /// Rotation to add on where the <see cref="InteriorObject"/>s should face relative to the root <see cref="InteriorObject"/>.<br/>
    /// This does affect its physical rotation (so it's applied to _clearancePositions and other things), and therefore should only be a multiple of 90 degrees.
    /// </summary>
    [Export(PropertyHint.Range, "0,270,90,radians")]
    public float RotationY { get; set; }
}
