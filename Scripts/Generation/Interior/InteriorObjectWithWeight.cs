using Godot;
using Scripts.Generation.Interior.Extension;
using System;

namespace Scripts.Generation.Interior;

[GlobalClass]
[Tool]
public partial class InteriorObjectWithWeight : Resource
{
    public InteriorObject InteriorObject { get; private set; }

    /// <summary>
    /// Use <see cref="InteriorObject"/> when this object is loaded.
    /// </summary>
    [Export(PropertyHint.File, "*.tres")]
    public string InteriorObjectPath
    {
        get => _interiorObjectPath;
        set
        {
            _interiorObjectPath = value;

            if (Addons.InteriorObjectCreator.Creator.HandlingResources || Engine.IsEditorHint()) { return; }
            LoadInteriorObject();
        }
    }
    private string _interiorObjectPath;

    /// <summary>
    /// Weight of appearance compared to other assigned <see cref="InteriorObjectWithWeight"/>s.
    /// Set to 0 to disable it from placement.
    /// </summary>
    [Export(PropertyHint.Range, "0,100,or_greater")]
    public int WeightOfPlacement { get; set; } = 1;

    private void LoadInteriorObject()
    {
        InteriorObject = GD.Load<InteriorObject>(InteriorObjectPath);

        if (InteriorObject is not InteriorObjectExtended iObjExt) { return; }
        iObjExt.LoadExtensions();
    }
}
