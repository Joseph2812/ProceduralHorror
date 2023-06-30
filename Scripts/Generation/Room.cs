using Godot;
using System;
using Scripts.Generation.Interior;

namespace Scripts.Generation;

public partial class Room : Resource
{
    [Export] public ItemManager.Id FloorId   { get; private set; }
    [Export] public ItemManager.Id WallId    { get; private set; }
    [Export] public ItemManager.Id CeilingId { get; private set; }

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ChanceOfEmptyCell { get; private set; } = 1; // Set (0 to 1). 1 for an empty room.

    public InteriorObject[] InteriorObjects { get; private set; } = Array.Empty<InteriorObject>();

    [Export(PropertyHint.ArrayType, "4/13:*.tres")] // Str/File
    private string[] _interiorObjectPaths
    {
        get => p_interiorObjectPaths;
        set
        {
            p_interiorObjectPaths = value;
            if (Engine.IsEditorHint()) { return; }

            InteriorObjects = CommonMethods.LoadPaths<InteriorObject>(value);
        }
    }
    private string[] p_interiorObjectPaths;
}
