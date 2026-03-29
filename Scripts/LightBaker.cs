using Godot;
using Scripts.Generation;
using System;

public partial class LightBaker : VoxelGI
{
    public override void _Ready()
    {
        base._Ready();

        MapGenerator.Inst.GenerationFinished += BakeVoxelGI;
    }

    private void BakeVoxelGI()
    {
        // Get bounds of Gridmap //
        Godot.Collections.Array<Vector3I> cells = MapGenerator.Inst.GetUsedCells();
        if (cells.Count == 0) { return; }

        Vector3I minCell = cells[0], maxCell = cells[0];
        foreach (Vector3I c in cells)
        {
            minCell = minCell.Min(c);
            maxCell = maxCell.Max(c);
        }
        Vector3 posMin = minCell * MapGenerator.Inst.CellSize;
        Vector3 posMax = (maxCell + Vector3I.One) * MapGenerator.Inst.CellSize;

        Size = posMax - posMin;
        Position = posMin + (Size * 0.5f);

        // Adjust subdivisions based on map size //
        const float Resolution = 2f; // metres per voxel
        float requiredSubdiv = Mathf.Max(Mathf.Max(Size.X, Size.Y), Size.Z) / Resolution;

        if      (requiredSubdiv <= 64f)  { Subdiv = SubdivEnum.Subdiv64; }
        else if (requiredSubdiv <= 128f) { Subdiv = SubdivEnum.Subdiv128; }
        else if (requiredSubdiv <= 256f) { Subdiv = SubdivEnum.Subdiv256; }
        else                             { Subdiv = SubdivEnum.Subdiv512; }
        //

        CallDeferred(VoxelGI.MethodName.Bake);
    }
}
