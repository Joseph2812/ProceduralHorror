using Godot;
using System;

namespace Scripts.Generation.Interior.Extension;

/// <summary>
/// Use to specify <see cref="InteriorObject"/>s and their shared placement positions. Root <see cref="InteriorObject"/>s use this data to randomly pick and place them relative to itself.
/// </summary>
[GlobalClass]
public partial class InteriorObjectExtension : Resource
{
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ChanceToSkipAPosition { get; private set; }

    [Export]
    public PlacementData[] PlacementData { get; private set; }

    /// <summary>
    /// Other <see cref="InteriorObject"/>s that pair with the root <see cref="InteriorObject"/> (meant to generate together). 
    /// </summary>
    public InteriorObjectWithWeight[] InteriorObjectWithWeightS { get; private set; }

    public InteriorObjectExtension() { CallDeferred(nameof(LoadInteriorObjectWithWeightS)); }

    private void LoadInteriorObjectWithWeightS()
    {
        InteriorObjectWithWeightS = CommonMethods.LoadSubDirectoryUpFromResource<InteriorObjectWithWeight>(ResourcePath, "IObjWithWeightS/");
    }
}
