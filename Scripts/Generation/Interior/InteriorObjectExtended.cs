using Godot;
using System;
using System.Collections.Generic;
using Scripts.Extensions;

namespace Scripts.Generation.Interior;

[Tool]
public partial class InteriorObjectExtended : InteriorObject
{
    private string[] _extensionPaths // Had to be done to allow cyclic references along extensions
    {
        get => p_extensionPaths;
        set
        {
            p_extensionPaths = value;
            if (Engine.IsEditorHint()) { return; }

            _extensions = CommonMethods.LoadPaths<InteriorObjectExtension>(value);
        }
    }
    private string[] p_extensionPaths;
    private InteriorObjectExtension[] _extensions;

    public override Variant _PropertyGetRevert(StringName property)
    {
        switch (property)
        {
            case nameof(_extensionPaths): return Array.Empty<string>();
            default                     : return base._PropertyGetRevert(property);
        }
    }
    public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetPropertyList()
    {
        Godot.Collections.Array<Godot.Collections.Dictionary> properties = new
        (
            new Godot.Collections.Dictionary[]
            {
                CommonMethods.GetCategory(nameof(InteriorObjectExtended)),
                new Godot.Collections.Dictionary
                {
                    { "name"       , nameof(_extensionPaths) },
                    { "type"       , (int)Variant.Type.PackedStringArray },
                    { "hint"       , (int)PropertyHint.ArrayType },
                    { "hint_string", $"{(int)Variant.Type.String}/{(int)PropertyHint.File}:*.tres" }
                }
            }
        );

        properties.AddRange(base._GetPropertyList());
        return properties;
    }

    public void CreateExtensionsRecursively(Vector3I pos, float rotationY)
    {
        for (int i = 0; i < _extensions.Length; i++)
        {
            InteriorObjectExtension extension = _extensions[i];
            for (int j = 0; j < extension.PlacementData.Length; j++)
            {
                if (MapGenerator.Inst.Rng.Randf() > extension.ChanceToExtend) { continue; }

                // Position & Rotation For Next Extension //
                PlacementData data = extension.PlacementData[j];
                Vector3I nextPos = pos + data.Position.RotatedY(rotationY);
                float nextRotationY = rotationY + data.RotationY;

                // Select Random InteriorObject For Placement //
                InteriorObject randomObj = extension.InteriorObjectsWithWeights.GetRandomElementByWeight(x => x.WeightOfPlacement).InteriorObject;

                if (!MapGenerator.Inst.TryCreateInteriorNode(randomObj, nextPos, nextRotationY)) { continue; }
                if (randomObj is InteriorObjectExtended extendedObj) { extendedObj.CreateExtensionsRecursively(nextPos, nextRotationY); }
            }
        }
    }
}
