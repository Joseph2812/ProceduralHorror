using Godot;
using System;
using Scripts.Generation.Interior;

namespace Scripts.Generation;

[GlobalClass]
public partial class Room : Resource
{
    [Export] public ItemManager.Id FloorId { get; private set; }
    [Export] public ItemManager.Id WallId { get; private set; }
    [Export] public ItemManager.Id CeilingId { get; private set; }

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ChanceOfEmptyCell { get; private set; } = 1; // Set to 1 for an empty room.

    [ExportGroup("Extrusions")]
    [ExportSubgroup("Outer Width (width either side of a doorway|centre)")]
    [Export(PropertyHint.Range, $"1,10,1,or_greater")]
    public int MinimumOuterWidth { get; private set; } = 1; // Minimum width: (1 * 2) + 1 = 3
    [Export(PropertyHint.Range, $"1,10,1,or_greater")]
    public int MaximumOuterWidth { get; private set; } = 1;

    [ExportSubgroup("Length")]
    [Export(PropertyHint.Range, $"3,10,1,or_greater")]
    public int MinimumLength { get; private set; } = 3;
    [Export(PropertyHint.Range, $"3,10,1,or_greater")]
    public int MaximumLength { get; private set; } = 3;

    [ExportSubgroup("Iterations")]
    [Export(PropertyHint.Range, $"1,10,1,or_greater")]
    public int MinimumExtrusionIterations { get; private set; } = 1;
    [Export(PropertyHint.Range, $"1,10,1,or_greater")]
    public int MaximumExtrusionIterations { get; private set; } = 1;

    [ExportGroup("Height")]
    [Export(PropertyHint.Range, $"3,10,1,or_greater")]
    public int MinimumHeight { get; private set; } = 3; // Includes ceiling
    [Export(PropertyHint.Range, $"3,10,1,or_greater")]
    public int MaximumHeight { get; private set; } = 3;

    [ExportGroup("Doorways")]
    [Export(PropertyHint.Range, $"1,10,1,or_greater")]
    public int MinimumDoorways { get; private set; } = 1;
    [Export(PropertyHint.Range, $"1,10,1,or_greater")]
    public int MaximumDoorways { get; private set; } = 1;

    public InteriorObjectWithWeight[] InteriorObjectWithWeightS { get; private set; }

    public Room() { CallDeferred(nameof(LoadInteriorObjectWithWeightS)); }

    private void LoadInteriorObjectWithWeightS()
    {
        if (ChanceOfEmptyCell == 1f) { return; }
        InteriorObjectWithWeightS = CommonMethods.LoadSubDirectoryUpFromResource<InteriorObjectWithWeight>(ResourcePath, "IObjWithWeightS/");
    }
}
