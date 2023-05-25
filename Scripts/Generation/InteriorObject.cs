using Godot;
using System;
using System.Collections.Generic;

namespace Scripts.Generation;

public partial class InteriorObject : Resource
{
    [Export]
    public PackedScene Scene { get; private set; }

    /// <summary>
    /// Likelihood of <c>Scene</c> appearing in a cell.
    /// </summary>
    [ExportGroup("Probability")]
    [Export]
    public int WeightOfAppearance { get; private set; }

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

    /// <summary>
    /// Minimum height <c>Scene</c> needs to be at/above for it to be placed.
    /// </summary>
    [ExportGroup("Constraints")]
    [Export]
    public int MinimumHeight { get; private set; }

    /// <summary>
    /// Maximum height <c>Scene</c> needs to be at/below for it to be placed.
    /// </summary>
    [Export]
    public int MaximumHeight { get; private set; }

    /// <summary>
    /// Used to mark what relative positions it will take up when placed. (0, 0, 0) will already be added for any <c>Scene</c>.<para/>
    /// Godot doesn't support exporting Vector3I[] :(
    /// </summary>
    [Export]
    private Vector3[] _clearancePositions = Array.Empty<Vector3>();

    /// <summary>
    /// Gets the positions that <c>Scene</c> would take up (or want clear) for being placed at <paramref name="originPos"/> with a <paramref name="rotationY"/> in radians.
    /// </summary>
    /// <param name="originPos">Position it will be placed at.</param>
    /// <param name="rotationY">Rotation applied around the global y-axis in radians.</param>
    /// <returns><c>Hashset</c> of positions that mark its requirement for placement.</returns>
    public HashSet<Vector3I> GetClearancePositions(Vector3I originPos, float rotationY)
    {
        HashSet<Vector3I> clearancePosS = new() { originPos };
        foreach (Vector3 relativePos in _clearancePositions)
        {
            clearancePosS.Add(originPos + GetRotatedPosition(relativePos, rotationY));
        }
        return clearancePosS;
    }

    protected Vector3I GetRotatedPosition(Vector3 relativePos, float rotationY) => (Vector3I)relativePos.Rotated(Vector3.Up, rotationY);
}
