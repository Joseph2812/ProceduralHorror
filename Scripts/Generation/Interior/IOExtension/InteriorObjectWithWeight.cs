using Godot;
using System;

namespace Scripts.Generation.Interior;

public partial class InteriorObjectWithWeight : Resource
{
    public InteriorObject InteriorObject { get; private set; }

    /// <summary>
    /// Weight of appearance compared to other assigned <see cref="InteriorObjectWithWeight"/>s.
    /// Set to 0 to disable it from placement.
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1,or_greater")]
    public int WeightOfPlacement { get; private set; } = 1;

    [Export(PropertyHint.File, "*.tres")]
    private string _interiorObjectPath; // Had to be done to allow cyclic references along extensions

    public InteriorObjectWithWeight()
    {
        // Causes a resource load error if it isn't loaded after rooms are loaded (Usually happens with cyclic references to resources. Godot doesn't like it for some reason)
        RoomManager.RoomsLoaded += () => InteriorObject = GD.Load<InteriorObject>(_interiorObjectPath);
    }
}
