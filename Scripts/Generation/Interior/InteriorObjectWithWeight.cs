using Godot;
using System;

namespace Scripts.Generation.Interior;

[GlobalClass]
public partial class InteriorObjectWithWeight : Resource
{
    public InteriorObject InteriorObject { get; private set; }

    [Export(PropertyHint.File, "*.tres")]
    private string _interiorObjectPath
    {
        get => p_interiorObjectPath;
        set
        {
            p_interiorObjectPath = value;
            CallDeferred(nameof(LoadInteriorObject)); // Load slightly later than other InteriorObject loading to avoid errors
        }
    }
    private string p_interiorObjectPath;

    /// <summary>
    /// Weight of appearance compared to other assigned <see cref="InteriorObjectWithWeight"/>s.
    /// Set to 0 to disable it from placement.
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1,or_greater")]
    public int WeightOfPlacement { get; private set; } = 1;

    private void LoadInteriorObject() { InteriorObject = GD.Load<InteriorObject>(_interiorObjectPath); }
}
