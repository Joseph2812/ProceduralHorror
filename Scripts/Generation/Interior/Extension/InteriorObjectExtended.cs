using Godot;
using System;
using System.Collections.Generic;
using Scripts.Extensions;
using System.Text;

namespace Scripts.Generation.Interior.Extension;

[GlobalClass]
[Tool]
public partial class InteriorObjectExtended : InteriorObject
{
    private InteriorObjectExtension[] _extensions;

    public InteriorObjectExtended()
    {
        if (Addons.InteriorObjectCreator.InteriorObjectCreator.HandlingResources || Engine.IsEditorHint()) { return; }
        CallDeferred(nameof(LoadExtensions));
    }

    public void CreateExtensionsRecursively(Vector3I pos, float rotationY)
    {
        for (int i = 0; i < _extensions.Length; i++)
        {
            InteriorObjectExtension extension = _extensions[i];
            for (int j = 0; j < extension.PlacementData.Length; j++)
            {
                if (MapGenerator.Inst.Rng.Randf() < extension.ChanceToSkipAPosition) { continue; }

                // Position & Rotation For Next Extension //
                PlacementData data = extension.PlacementData[j];
                Vector3I nextPos = pos + data.Position.RotatedY(rotationY);
                float nextRotationY = rotationY + data.RotationY;

                // Select Random InteriorObject For Placement //
                InteriorObject randomObj = extension.InteriorObjectWithWeightS.GetRandomElementByWeight(x => x.WeightOfPlacement).InteriorObject;

                if (!MapGenerator.Inst.TryCreateInteriorNode(randomObj, nextPos, nextRotationY)) { continue; }
                if (randomObj is InteriorObjectExtended extendedObj) { extendedObj.CreateExtensionsRecursively(nextPos, nextRotationY); }
            }
        }
    }

    private void LoadExtensions()
    {
        string extensionsDir = CommonMethods.GetPathWithoutEndDirectory(ResourcePath) + "Extensions/";
        string[] subDirs = DirAccess.GetDirectoriesAt(extensionsDir);

        _extensions = new InteriorObjectExtension[subDirs.Length];
        for (int i = 0; i < subDirs.Length; i++)
        {
            StringBuilder strBuilder = new(extensionsDir);
            strBuilder.Append(subDirs[i]);
            strBuilder.Append('/');
            strBuilder.Append(DirAccess.GetFilesAt(strBuilder.ToString())[0]);

            _extensions[i] = GD.Load<InteriorObjectExtension>(strBuilder.ToString());
        }
    }
}
