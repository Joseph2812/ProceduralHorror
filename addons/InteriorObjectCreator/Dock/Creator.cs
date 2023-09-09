#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using Scripts.Generation.Interior;
using Scripts.Generation.Interior.Extension;
using System.IO;

namespace Addons.InteriorObjectCreator;

[Tool]
public partial class Creator : VBoxContainer
{
    public const string InteriorObjectDirectory = "res://Generation/InteriorObjects/";
    public const string InteriorObjectFileName = "InteriorObject.tres";

    private const string ExtensionFileName = "Extension.tres";
    private const string ExtensionsDirName = "Extensions";
    private const string IObjWithWeightSDirName = "IObjWithWeightS";

    public static bool HandlingResources { get; private set; }

    private PackedScene _vector3Element;
    private PackedScene _extensionElement;
    private PackedScene _placementDataElement;
    private PackedScene _iObjWithWeightElement;

    // UI Elements //
    private LineEdit _name;
    private LineEdit _scenePath;

    private LineEdit _weightToMiddle;
    private CheckBox _exact;

    private LineEdit _minimumHeight;
    private LineEdit _maximumHeight;
    private OptionButton _relativeTo;
    private LineEdit _maximumCountBtwRooms;
    private TextEdit _neighbourConditionsText;
    private UiList _clearancePositions;
    
    private UiList _semiClearancePositions; 

    private LineEdit _minimumRotationalYOffset;
    private LineEdit _maximumRotationalYOffset;
    //

    private ExtensionReferences _extReferences;
    private readonly Dictionary<Node, UiElements.Vector3> _nodeToClearancePositions = new();
    private readonly Dictionary<Node, UiElements.Vector3> _nodeToSemiClearancePositions = new();

    private StringName _dropType = "files_and_dirs";

    public override void _Ready()
    {
        base._Ready();

        _vector3Element = GD.Load<PackedScene>("res://addons/InteriorObjectCreator/Dock/Elements/Element_Vector3.tscn");
        _extensionElement = GD.Load<PackedScene>("res://addons/InteriorObjectCreator/Dock/Elements/Element_Extension.tscn");
        _placementDataElement = GD.Load<PackedScene>("res://addons/InteriorObjectCreator/Dock/Elements/Element_PlacementData.tscn");
        _iObjWithWeightElement = GD.Load<PackedScene>("res://addons/InteriorObjectCreator/Dock/Elements/Element_IObjWithWeight.tscn");

        // UI Elements //
        Node iObjStart = GetNode<Node>("TabContainer/InteriorObject/VBoxContainer");
        _name      = iObjStart.GetNode<LineEdit>("Name/LineEdit");
        _scenePath = iObjStart.GetNode<LineEdit>("ScenePath/LineEdit");

        Node probability = iObjStart.GetNode<Node>("Probability/VBoxContainer");
        _weightToMiddle = probability.GetNode<LineEdit>("WeightToMiddle/LineEdit");
        _exact          = probability.GetNode<CheckBox>("Exact/CheckBox");

        Node constraints = iObjStart.GetNode<Node>("Constraints/VBoxContainer");
        _minimumHeight           = constraints.GetNode<LineEdit>("MinimumHeight/LineEdit");
        _maximumHeight           = constraints.GetNode<LineEdit>("MaximumHeight/LineEdit");
        _relativeTo              = constraints.GetNode<OptionButton>("RelativeTo/OptionButton");
        _maximumCountBtwRooms    = constraints.GetNode<LineEdit>("MaximumCountBtwRooms/LineEdit");
        _neighbourConditionsText = constraints.GetNode<TextEdit>("NeighbourConditionsText/TextEdit");
        _clearancePositions      = constraints.GetNode<UiList>("ClearancePositions/UiList");
        _semiClearancePositions  = constraints.GetNode<UiList>("SemiClearancePositions/UiList");

        Node rotation = iObjStart.GetNode<Node>("Rotation/VBoxContainer");
        _minimumRotationalYOffset = rotation.GetNode<LineEdit>("MinimumRotationalYOffset/LineEdit");
        _maximumRotationalYOffset = rotation.GetNode<LineEdit>("MaximumRotationalYOffset/LineEdit");
        //

        _extReferences = GetNode<ExtensionReferences>("TabContainer/Extensions/ExtensionList");

        GetNode<BaseButton>("Button").Pressed += OnCreate_Pressed;
        _clearancePositions.Creation += OnClearancePositions_Creation;
        _clearancePositions.Deletion += OnClearancePositions_Deletion;
        _semiClearancePositions.Creation += OnSemiClearancePositions_Creation;
        _semiClearancePositions.Deletion += OnSemiClearancePositions_Deletion;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        Godot.Collections.Dictionary dict;
        if (data.VariantType == Variant.Type.Dictionary) { dict = data.AsGodotDictionary(); }
        else                                             { return false; }
        
        return
        (
            dict.ContainsKey("files") &&
            Godot.FileAccess.FileExists(data.AsGodotDictionary()["files"].AsStringArray()[0] + InteriorObjectFileName)
        );
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        Godot.Collections.Dictionary dict = data.AsGodotDictionary();      
        if (dict["type"].AsStringName() == _dropType)
        {
            string objDir = dict["files"].AsStringArray()[0];

            HandlingResources = true;
            try
            {
                InteriorObject iObj = GD.Load<InteriorObject>(objDir + InteriorObjectFileName);
                LoadInteriorObject(iObj, objDir);
                LoadExtensions(objDir);
            }
            catch (Exception e)
            {
                GD.PushError($"Failed to load data in \"{objDir}\". Exception: {e.Message}");
            }
            HandlingResources = false;
        }
    }
   
    private void SetupInteriorObject(DirAccess dir, InteriorObject iObj)
    {
        int intResult;
        float floatResult;
        Vector3[] positions;

        // Scene //
        iObj.Scene = GD.Load<PackedScene>(_scenePath.Text);
        if (iObj.Scene == null) { throw new Exception($"Failed to load \"{_scenePath.Text}\". Make sure it's a valid absolute path."); }

        // Weight To Middle //
        if (float.TryParse(_weightToMiddle.Text, out floatResult))
        {
            iObj.WeightToMiddle = floatResult;
        }
        else { PrintParseWarning(nameof(_weightToMiddle)); }

        // Exact //
        iObj.Exact = _exact.ButtonPressed;

        // Minimum Height //
        if (int.TryParse(_minimumHeight.Text, out intResult))
        {
            iObj.MinimumHeight = intResult;
        }
        else { PrintParseWarning(nameof(_minimumHeight)); }

        // Maximum Height //
        if (int.TryParse(_maximumHeight.Text, out intResult))
        {
            iObj.MaximumHeight = intResult;
        }
        else { PrintParseWarning(nameof(_maximumHeight)); }

        // Relative To //
        iObj.RelativeTo = (InteriorObject.Relative)_relativeTo.Selected;

        // Maximum Count Between Rooms //
        if (int.TryParse(_maximumCountBtwRooms.Text, out intResult))
        {
            iObj.MaximumCountBtwRooms = intResult;
        }
        else { PrintParseWarning(nameof(_maximumCountBtwRooms)); }

        // Neighbour Conditions Text //
        iObj.NeighbourConditionsText = _neighbourConditionsText.Text;

        // Clearance Positions //
        if (!GetPositionArray(_nodeToClearancePositions, out positions)) { PrintParseWarning(nameof(_clearancePositions)); }
        iObj.ClearancePositions = positions;

        // Semi-clearance Positions //
        if (!GetPositionArray(_nodeToSemiClearancePositions, out positions)) { PrintParseWarning(nameof(_semiClearancePositions)); }
        iObj.SemiClearancePositions = positions;

        // Minimum Rotational-Y Offset //
        if (float.TryParse(_minimumRotationalYOffset.Text, out floatResult))
        {
            iObj.MinimumRotationalYOffset = Mathf.DegToRad(floatResult);
        }
        else { PrintParseWarning(nameof(_minimumRotationalYOffset)); }

        // Maximum Rotational-Y Offset //
        if (float.TryParse(_maximumRotationalYOffset.Text, out floatResult))
        {
            iObj.MaximumRotationalYOffset = Mathf.DegToRad(floatResult);
        }
        else { PrintParseWarning(nameof(_maximumRotationalYOffset)); }

        ResourceSaver.Save(iObj, $"{dir.GetCurrentDir()}/{InteriorObjectFileName}");
    }
    private void SetupExtensions(DirAccess dir, List<UiElements.Extension> validExtensions)
    {
        dir.MakeDir(ExtensionsDirName);
        for (int i = 0; i < validExtensions.Count; i++)
        {
            UiElements.Extension extensionRef = validExtensions[i];

            string extensionDir = $"{ExtensionsDirName}/{i}";
            dir.MakeDir(extensionDir);

            string iObjWithWeightDir = $"{extensionDir}/{IObjWithWeightSDirName}";
            dir.MakeDir(iObjWithWeightDir);

            // Extension //
            InteriorObjectExtension iObjExtension = new();

            if (float.TryParse(extensionRef.ChanceToSkipAPosition.Text, out float result))
            {
                iObjExtension.ChanceToSkipAPosition = result;
            }
            else { PrintParseWarning(nameof(iObjExtension.ChanceToSkipAPosition)); }

            if (!extensionRef.GetPlacementData(out PlacementData[] datas)) { PrintParseWarning(nameof(iObjExtension.PlacementData)); }
            iObjExtension.PlacementData = datas;

            ResourceSaver.Save(iObjExtension, $"{dir.GetCurrentDir()}/{extensionDir}/{ExtensionFileName}");

            // InteriorObjectWithWeight Directory Inside Extension //
            if (!extensionRef.GetIObjWithWeightS(out InteriorObjectWithWeight[] iObjWithWeightS)) { PrintParseWarning(nameof(iObjExtension.InteriorObjectWithWeightS)); }

            foreach (InteriorObjectWithWeight iObjWithWeight in iObjWithWeightS)
            {
                ResourceSaver.Save(iObjWithWeight, $"{dir.GetCurrentDir()}/{iObjWithWeightDir}/{Directory.GetParent(iObjWithWeight.InteriorObjectPath).Name}.tres");
            }
        }
    }

    private void LoadInteriorObject(InteriorObject iObj, string objDir)
    {
        _name.Text = GetPathRelativeToInteriorObjectsDir(objDir);
        _scenePath.Text = iObj.Scene.ResourcePath;

        _weightToMiddle.Text = iObj.WeightToMiddle.ToString();
        _exact.ButtonPressed = iObj.Exact;

        _minimumHeight.Text = iObj.MinimumHeight.ToString();
        _maximumHeight.Text = iObj.MaximumHeight.ToString();
        _relativeTo.Selected = (int)iObj.RelativeTo;
        _maximumCountBtwRooms.Text = iObj.MaximumCountBtwRooms.ToString();
        _neighbourConditionsText.Text = iObj.NeighbourConditionsText;
        SetVector3UiList(_clearancePositions, _nodeToClearancePositions, iObj.ClearancePositions);
        SetVector3UiList(_semiClearancePositions, _nodeToSemiClearancePositions, iObj.SemiClearancePositions);

        _minimumRotationalYOffset.Text = Mathf.RadToDeg(iObj.MinimumRotationalYOffset).ToString();
        _maximumRotationalYOffset.Text = Mathf.RadToDeg(iObj.MaximumRotationalYOffset).ToString();
    }
    private void LoadExtensions(string objDir)
    {
        _extReferences.Clear();

        DirAccess dir = DirAccess.Open($"{objDir}/{ExtensionsDirName}");
        if (dir == null) { return; }

        int i = 0;
        while (dir.ChangeDir(i++.ToString()) == Error.Ok)
        {
            Node node = _extensionElement.Instantiate();
            _extReferences.Add(node, true);

            UiElements.Extension extensionRef = _extReferences[node];
            InteriorObjectExtension extension = GD.Load<InteriorObjectExtension>($"{dir.GetCurrentDir()}/{ExtensionFileName}");

            // Get ChanceToSkipAPosition & PlacementData //
            extensionRef.ChanceToSkipAPosition.Text = extension.ChanceToSkipAPosition.ToString();
            SetPlacementDataUiList(extensionRef, extension.PlacementData);

            // Get InteriorObjectWithWeightS //
            dir.ChangeDir(IObjWithWeightSDirName);
            string[] fileNames = dir.GetFiles();
            
            InteriorObjectWithWeight[] iObjWithWeightS = new InteriorObjectWithWeight[fileNames.Length];
            for (int j = 0; j < iObjWithWeightS.Length; j++)
            {
                iObjWithWeightS[j] = GD.Load<InteriorObjectWithWeight>($"{dir.GetCurrentDir()}/{fileNames[j]}");
            }
            SetIObjWithWeightSUiList(extensionRef, iObjWithWeightS);
            //

            dir.ChangeDir("../..");
        }
    }

    private void SetVector3UiList(UiList uiList, Dictionary<Node, UiElements.Vector3> dict, Vector3[] positions)
    {
        uiList.Clear();
        foreach (Vector3 pos in positions)
        {
            Node node = _vector3Element.Instantiate();
            uiList.Add(node, true);

            UiElements.Vector3 element = dict[node];
            element.X.Text = pos.X.ToString();
            element.Y.Text = pos.Y.ToString();
            element.Z.Text = pos.Z.ToString();
        }
    }
    private void SetPlacementDataUiList(UiElements.Extension extensionRef, PlacementData[] datas)
    {
        extensionRef.PlacementDataList.Clear();
        foreach (PlacementData data in datas)
        {
            Node node = _placementDataElement.Instantiate();
            extensionRef.PlacementDataList.Add(node, true);

            UiElements.PlacementData element = extensionRef.GetPlacementDataRef(node);
            element.Position.X.Text = data.Position.X.ToString();
            element.Position.Y.Text = data.Position.Y.ToString();
            element.Position.Z.Text = data.Position.Z.ToString();
            element.RotationY.Text = Mathf.RadToDeg(data.RotationY).ToString();
        }
    }
    private void SetIObjWithWeightSUiList(UiElements.Extension extensionRef, InteriorObjectWithWeight[] iObjWithWeightS)
    {
        extensionRef.IObjWithWeightList.Clear();
        foreach (InteriorObjectWithWeight iObjWt in iObjWithWeightS)
        {
            Node node = _iObjWithWeightElement.Instantiate();
            extensionRef.IObjWithWeightList.Add(node, true);

            UiElements.InteriorObjectWithWeight element = extensionRef.GetIObjWithWeightRef(node);
            element.Name.Text = GetPathRelativeToInteriorObjectsDir(Directory.GetParent(iObjWt.InteriorObjectPath).FullName);
            element.WeightOfPlacement.Text = iObjWt.WeightOfPlacement.ToString();
        }
    }

    private string GetPathRelativeToInteriorObjectsDir(string fullPath)
    {
        int idx = fullPath.Find("InteriorObjects") + 16;
        return fullPath[idx..];
    }

    private bool GetPositionArray(Dictionary<Node, UiElements.Vector3> dict, out Vector3[] positions)
    {
        positions = new Vector3[dict.Count];

        int i = 0;
        bool success = true;

        foreach (KeyValuePair<Node, UiElements.Vector3> kv in dict)
        {
            success &= float.TryParse(kv.Value.X.Text, out float x);
            success &= float.TryParse(kv.Value.Y.Text, out float y);
            success &= float.TryParse(kv.Value.Z.Text, out float z);

            positions[i++] = new Vector3(x, y, z);
        }
        return success;
    }

    private void PrintParseWarning(string name) { GD.PushWarning($"Couldn't parse \"{name}\" input(s). Leaving as default. Ignore if this is intended."); }

    private void OnCreate_Pressed()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            GD.PushError("Name must be entered. e.g. Directory/IObjDir.");
            return;
        }

        // Create Root Directory //
        DirAccess dir = DirAccess.Open(InteriorObjectDirectory);
        if (dir.DirExists(_name.Text)) { OS.MoveToTrash(ProjectSettings.GlobalizePath(dir.GetCurrentDir() + $"/{_name.Text}")); }

        dir.MakeDirRecursive(_name.Text);
        dir.ChangeDir(_name.Text);

        // Get Valid Extensions //
        List<UiElements.Extension> validExtensions = new();

        IEnumerator<KeyValuePair<Node, UiElements.Extension>> extensions = _extReferences.GetNodeToExtensionS();
        while (extensions.MoveNext())
        {
            UiElements.Extension extension = extensions.Current.Value;
            if (extension.PlacementDataCount > 0 && extension.IObjWithWeightCount > 0)
            {
                validExtensions.Add(extension);
            }
        }

        // Create & Save Resources //
        HandlingResources = true;
        try
        {
            InteriorObject iObj;
            if (validExtensions.Count == 0) { iObj = new(); }
            else
            {
                iObj = new InteriorObjectExtended();
                SetupExtensions(dir, validExtensions);
            }
            SetupInteriorObject(dir, iObj);
        }
        catch (Exception e)
        {
            GD.PushError($"Failed to setup {nameof(InteriorObject)} files. Exception: {e.Message}");
        }
        HandlingResources = false;
    }

    private void OnClearancePositions_Creation(Node node) { _nodeToClearancePositions.Add(node, new(node)); }
    private void OnClearancePositions_Deletion(Node node) { _nodeToClearancePositions.Remove(node); }

    private void OnSemiClearancePositions_Creation(Node node) { _nodeToSemiClearancePositions.Add(node, new(node)); }
    private void OnSemiClearancePositions_Deletion(Node node) { _nodeToSemiClearancePositions.Remove(node); }
}
#endif