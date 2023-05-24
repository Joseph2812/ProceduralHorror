using Godot;
using System;
using System.Collections.Generic;

namespace Scripts.Generation;

public partial class InteriorObjectExtended : InteriorObject
{
    /// <summary>
    /// Other <c>InteriorObject</c>s that pairs with this <c>InteriorObject</c> (meant to generate together). 
    /// </summary>
    [Export]
    private InteriorObject[] _extensions = null;

    /// <summary>
    /// Where should all the <c>_extensions</c> be placed relative to this <c>InteriorObject</c>.
    /// </summary>
    [Export]
    private Vector3[] _placementPositions = null;

    public void CreateExtensionsRecursively(Vector3I originPos, float rotationY)
    {
        foreach (Vector3 relativePos in _placementPositions)
        {
            Vector3I nextPos = originPos + GetRotatedPosition(relativePos, rotationY);

            InteriorObject randomObj = _extensions[GridGenerator.Inst.Rng.RandiRange(0, _extensions.Length - 1)];
            HashSet<Vector3I> clearancePositions = randomObj.GetClearancePositions(nextPos, rotationY);

            if (!GridGenerator.Inst.IsPlacementValid(clearancePositions)) { continue; }
            GridGenerator.Inst.CreateInteriorNode(randomObj.Scene, nextPos, rotationY, clearancePositions);

            if (randomObj is InteriorObjectExtended extendedObj) { extendedObj.CreateExtensionsRecursively(nextPos, rotationY); }
        }
    }
}
