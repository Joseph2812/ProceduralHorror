using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

namespace Scripts.Generation;

public partial class InteriorObjectExtended : InteriorObject
{
    /// <summary>
    /// Other <c>InteriorObject</c>s that pairs with this <c>InteriorObject</c> (meant to generate together). 
    /// </summary>
    [Export]
    private Array<Array<InteriorObject>> _extensions = null;

    /// <summary>
    /// Where should all the <c>_extensions</c> be placed relative to this <c>InteriorObject</c>.
    /// </summary>
    [Export]
    private Array<Vector3[]> _placementPositions = null;

    public void CreateExtensionsRecursively(Vector3I originPos, float rotationY)
    {
        for (int i = 0; i < _extensions.Count; i++)
        {
            for (int j = 0; j < _placementPositions.Count; j++)
            {
                Vector3I nextPos = originPos + GetRotatedPosition(_placementPositions[i][j], rotationY);

                InteriorObject randomObj = _extensions[i][GridGenerator.Inst.Rng.RandiRange(0, _extensions[i].Count - 1)];
                HashSet<Vector3I> clearancePositions = randomObj.GetClearancePositions(nextPos, rotationY);

                if (!GridGenerator.Inst.IsPlacementValid(clearancePositions)) { continue; }
                GridGenerator.Inst.CreateInteriorNode(randomObj.Scene, nextPos, rotationY, clearancePositions);

                if (randomObj is InteriorObjectExtended extendedObj) { extendedObj.CreateExtensionsRecursively(nextPos, rotationY); }
            }
        }
    }
}
