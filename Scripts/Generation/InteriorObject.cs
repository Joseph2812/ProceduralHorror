using Godot;
using System;

namespace Scripts.Generation;

public partial class InteriorObject : Resource
{
    [Export]
    public PackedScene Scene { get; private set; }

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
    /// If true, <c>Scene</c> will have a chance to appear in a cell only if <c>WeightToCentre</c> is equal to normalised distance.<para></para>
    /// Mainly only useful for <c>WeightToCentre</c> = 0 or 1, as inbetween values aren't guaranteed to appear among the cells.
    /// </summary>
    [Export]
    public bool Exact { get; private set; }
}
