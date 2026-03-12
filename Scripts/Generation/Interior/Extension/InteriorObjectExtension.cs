using Godot;
using System;

namespace Scripts.Generation.Interior.Extension;

/// <summary>
/// Use to specify <see cref="InteriorObject"/>s and their shared placement positions. Root <see cref="InteriorObject"/>s use this data to randomly pick and place them relative to itself.
/// </summary>
[GlobalClass]
[Tool]
public partial class InteriorObjectExtension : Resource
{
    [Export(PropertyHint.Range, "0,1")]
    public float ChanceToSkipAPosition { get; set; }

    [Export]
    public PlacementData[] PlacementData { get; set; }

    /// <summary>
    /// Other <see cref="InteriorObject"/>s that pair with the root <see cref="InteriorObject"/> (meant to generate together). 
    /// </summary>
    public InteriorObjectWithWeight[] InteriorObjectWithWeight_S { get; private set; }

    public void LoadDependencies()
    {
        InteriorObjectWithWeight_S = CommonMethod.LoadSubDirectoryNextToPath<InteriorObjectWithWeight>(ResourcePath, "IObjWithWeightS/"); // TODO: Rename to IObjWithWeight_S
        foreach (InteriorObjectWithWeight iObjWithWt in InteriorObjectWithWeight_S) { iObjWithWt.LoadDependencies(); }
    }
}
