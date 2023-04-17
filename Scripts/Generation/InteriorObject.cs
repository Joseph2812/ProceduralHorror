using Godot;
using System;

namespace Scripts.Generation;

public partial class InteriorObject : Resource
{
    [Export]
    public PackedScene Scene { get; private set; }

    /// <summary>
    /// Likelihood of appearing on a cell, relative to the edge of a room.
    /// </summary>
    [Export(PropertyHint.Range, "0, 1")]
    public float WeightToEdge { get; private set; }

    /// <summary>
    /// Distance outward from weighted position that this scene can be placed on (0 = only the closest cell the weight value is on).
    /// </summary>
    public int WeightInfluence { get; private set; }

    /// <summary>
    /// Amount that can appear in one room.
    /// </summary>
    public int MaxCount { get; private set; }
}
