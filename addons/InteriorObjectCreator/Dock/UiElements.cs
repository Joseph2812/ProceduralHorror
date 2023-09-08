#if TOOLS
using Godot;
using System;
using System.Collections.Generic;

namespace Addons.InteriorObjectCreator.UiElements;

public class Extension
{
    public readonly LineEdit ChanceToSkipAPosition;
    public readonly UiList PlacementDataList;
    public readonly UiList IObjWithWeightList;

    private readonly Dictionary<Node, PlacementData> _nodeToPlacementData = new();
    private readonly Dictionary<Node, InteriorObjectWithWeight> _nodeToIObjWithWeight = new();

    public Extension(Node root)
    {
        ChanceToSkipAPosition = root.GetNode<LineEdit>("ChanceToSkipAPosition/LineEdit");
        PlacementDataList = root.GetNode<UiList>("PlacementData/UiList");
        IObjWithWeightList = root.GetNode<UiList>("IObjWithWeightS/UiList");

        PlacementDataList.Creation += OnPlacementDataList_Creation;
        PlacementDataList.Deletion += OnPlacementDataList_Deletion;

        IObjWithWeightList.Creation += OnIObjWithWeightList_Creation;
        IObjWithWeightList.Deletion += OnIObjWithWeightList_Deletion;
    }

    public int PlacementDataCount => _nodeToPlacementData.Count;
    public int IObjWithWeightCount => _nodeToIObjWithWeight.Count;

    public PlacementData GetPlacementDataRef(Node node) => _nodeToPlacementData[node];
    public bool GetPlacementData(out Scripts.Generation.Interior.Extension.PlacementData[] datas)
    {
        int i = 0;
        bool success = true;
        datas = new Scripts.Generation.Interior.Extension.PlacementData[_nodeToPlacementData.Count];

        foreach (KeyValuePair<Node, PlacementData> nodeToPlacementData in _nodeToPlacementData)
        {
            PlacementData dataRef = nodeToPlacementData.Value;

            success &= int.TryParse(dataRef.Position.X.Text, out int x);
            success &= int.TryParse(dataRef.Position.Y.Text, out int y);
            success &= int.TryParse(dataRef.Position.Z.Text, out int z);
            success &= float.TryParse(dataRef.RotationY.Text, out float rotY);

            Scripts.Generation.Interior.Extension.PlacementData data = new();
            data.Position = new Vector3I(x, y, z);
            data.RotationY = Mathf.DegToRad(rotY);

            datas[i++] = data;
        }
        return success;
    }

    public InteriorObjectWithWeight GetIObjWithWeightRef(Node node) => _nodeToIObjWithWeight[node];
    public bool GetIObjWithWeightS(out Scripts.Generation.Interior.InteriorObjectWithWeight[] iObjWithWeightS)
    {
        int i = 0;
        bool success = true;
        iObjWithWeightS = new Scripts.Generation.Interior.InteriorObjectWithWeight[_nodeToIObjWithWeight.Count];

        foreach (KeyValuePair<Node, InteriorObjectWithWeight> nodeToIObjWithWeight in _nodeToIObjWithWeight)
        {
            InteriorObjectWithWeight iObjWithWeightRef = nodeToIObjWithWeight.Value;

            bool parsed = int.TryParse(iObjWithWeightRef.WeightOfPlacement.Text, out int weightOfPlacement);
            success &= parsed;

            // Set Properties //
            Scripts.Generation.Interior.InteriorObjectWithWeight iObjWithWeight = new();

            iObjWithWeight.InteriorObjectPath = $"{InteriorObjectCreator.InteriorObjectDirectory}{iObjWithWeightRef.Name.Text}/{InteriorObjectCreator.InteriorObjectFileName}";
            if (!FileAccess.FileExists(iObjWithWeight.InteriorObjectPath))
            {
                throw new Exception($"\"{iObjWithWeight.InteriorObjectPath}\" of {nameof(iObjWithWeight.InteriorObjectPath)} is not a valid path");
            }
            if (parsed) { iObjWithWeight.WeightOfPlacement = weightOfPlacement; }
            //

            iObjWithWeightS[i++] = iObjWithWeight;
        }
        return success;
    }

    private void OnPlacementDataList_Creation(Node node) { _nodeToPlacementData.Add(node, new(node)); }
    private void OnPlacementDataList_Deletion(Node node) { _nodeToPlacementData.Remove(node); }

    private void OnIObjWithWeightList_Creation(Node node) { _nodeToIObjWithWeight.Add(node, new(node)); }
    private void OnIObjWithWeightList_Deletion(Node node) { _nodeToIObjWithWeight.Remove(node); }
}

public struct Vector3
{
    public readonly LineEdit X, Y, Z;

    public Vector3(Node root)
    {
        X = root.GetNode<LineEdit>("X/LineEdit");
        Y = root.GetNode<LineEdit>("Y/LineEdit");
        Z = root.GetNode<LineEdit>("Z/LineEdit");
    }
}

public struct PlacementData
{
    public readonly Vector3 Position;
    public readonly LineEdit RotationY;

    public PlacementData(Node root)
    {
        Position = new(root.GetNode<Node>("Element_Vector3"));
        RotationY = root.GetNode<LineEdit>("RotationY/LineEdit");
    }
}
public struct InteriorObjectWithWeight
{
    public readonly LineEdit Name;
    public readonly LineEdit WeightOfPlacement;

    public InteriorObjectWithWeight(Node root)
    {
        Name = root.GetNode<LineEdit>("Name/LineEdit");
        WeightOfPlacement = root.GetNode<LineEdit>("WeightOfPlacement/LineEdit");
    }
}
#endif