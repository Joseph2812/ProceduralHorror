using Godot;
using System;
using System.Collections.Generic;

namespace Scripts.Generation;

public partial class InteriorObject : Resource
{
    [Export]
    public PackedScene Scene { get; private set; }

    [ExportGroup("Probability")]

    /// <summary>
    /// Likelihood of <c>Scene</c> appearing in a cell.
    /// </summary>
    [Export(PropertyHint.Range, "0, 1")]
    public float Rarity { get; private set; }

    /// <summary>
    /// Likelihood of <c>Scene</c> appearing in a cell depending on its proxmity to the centre, from 0 (edge) to 1 (centre).
    /// </summary>
    [Export(PropertyHint.Range, "0, 1")]
    public float WeightToCentre { get; private set; }

    /// <summary>
    /// If true, <c>Scene</c> will have a chance to appear in a cell only if <c>WeightToCentre</c> is equal to normalised distance.<para/>
    /// Mainly only useful for <c>WeightToCentre</c> = 0 or 1, as inbetween values aren't guaranteed to appear among the cells.
    /// </summary>
    [Export]
    public bool Exact { get; private set; }

    [ExportGroup("Placement")]

    /// <summary>
    /// Offset applied to <c>Scene</c> so the north-west corner of it lines up with the origin.
    /// </summary>
    [Export]
    private Vector3 _offset;

    /// <summary>
    /// Used to mark what relative positions it will take up when placed. (0, 0, 0) is already checked by <c>GridGenerator</c> so it isn't needed.<para/>
    /// Godot doesn't support exporting Vector3I[] :(
    /// </summary>
    [Export]
    private Vector3[] _clearancePositions = Array.Empty<Vector3>();

    /// <returns>Offset rotated by <paramref name="rotationY"/> in radians.</returns>
    public Vector3 GetOffset(float rotationY) => _offset.Rotated(Vector3I.Up, rotationY).Abs();

    /// <summary>
    /// Gets the positions that <c>Scene</c> would take up (or want clear) for being placed at <paramref name="originPos"/> with a <paramref name="rotationY"/> in radians.
    /// </summary>
    /// <param name="originPos">Position it will be placed at.</param>
    /// <param name="rotationY">Rotation applied around the global y-axis in radians.</param>
    /// <returns><c>Hashset</c> of positions that mark its requirement for placement.</returns>
    public HashSet<Vector3I> GetClearancePositions(Vector3I originPos, float rotationY)
    {
        HashSet<Vector3I> clearancePosS = new();
        foreach (Vector3 relativePos in _clearancePositions)
        {
            clearancePosS.Add(originPos + (Vector3I)relativePos.Rotated(Vector3I.Up, rotationY));
        }
        return clearancePosS;
    }
}
