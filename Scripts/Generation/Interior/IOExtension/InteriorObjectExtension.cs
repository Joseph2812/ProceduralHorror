using Godot;
using System;

namespace Scripts.Generation.Interior;

/// <summary>
/// Use to specify <see cref="InteriorObject"/>s and their shared placement positions. Root <see cref="InteriorObject"/>s use this data to randomly pick and place them relative to itself.
/// </summary>
public partial class InteriorObjectExtension : Resource
{
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ChanceToExtend { get; private set; } = 1f;

    [Export]
    public PlacementData[] PlacementData { get; private set; }

    /// <summary>
    /// Other <see cref="InteriorObject"/>s that pair with the root <see cref="InteriorObject"/> (meant to generate together). 
    /// </summary>
    [Export]
    public InteriorObjectWithWeight[] InteriorObjectsWithWeights { get; private set; }
}
