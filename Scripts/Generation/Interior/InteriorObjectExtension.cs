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
    public InteriorObject[] InteriorObjects { get; private set; }

    [Export(PropertyHint.ArrayType, "4/13:*.tres")] // Str/File
    private string[] _interiorObjectPaths; // Had to be done to allow cyclic references along extensions

    public InteriorObjectExtension()
    {
        // Causes a resource load error if it isn't loaded after rooms (Usually happens with cyclic references to resources. Godot doesn't like it for some reason)
        RoomManager.RoomsLoaded += () => InteriorObjects = CommonMethods.LoadPaths<InteriorObject>(_interiorObjectPaths);
    }
}
