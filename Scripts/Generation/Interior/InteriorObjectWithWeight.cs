using Godot;
using System;

namespace Scripts.Generation.Interior;

[GlobalClass]
[Tool]
public partial class InteriorObjectWithWeight : Resource
{
    /// <summary>
    /// Use <see cref="InteriorObject"/> when this object is loaded.
    /// </summary>
    [Export(PropertyHint.File, "*.tres")]
    public string InteriorObjectPath { get; set; }

    /// <summary>
    /// Weight of appearance compared to other assigned <see cref="InteriorObjectWithWeight"/>s.
    /// Set to 0 to disable it from placement.
    /// </summary>
    [Export(PropertyHint.Range, "0,100,or_greater")]
    public int WeightOfPlacement { get; set; } = 1;

    public InteriorObject InteriorObject { get; private set; }

    public void LoadDependencies()
    {
        InteriorObject = GD.Load<InteriorObject>(InteriorObjectPath);
        InteriorObject.CheckDependencies();
    }
}
