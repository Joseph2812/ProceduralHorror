#if TOOLS
using Godot;
using System.Collections.Generic;

namespace Addons.InteriorObjectCreator;

[Tool]
public partial class ExtensionSelector : VBoxContainer
{
    private class Extension
    {
        public readonly LineEdit X;
        public readonly LineEdit Y;
        public readonly LineEdit Z;
        public readonly LineEdit RotationY;
        public readonly Button SelectorButton;

        private readonly Dictionary<Node, IObjWithWeight> _iObjWithWeightS = new();
        private readonly UiList _iObjWithWeightRoot;

        public Extension(Node root, UiList iObjWithWeightRoot)
        {
            X = root.GetNode<LineEdit>("HBoxContainer/VBoxContainer/Element_Vector3/X/LineEdit");
            Y = root.GetNode<LineEdit>("HBoxContainer/VBoxContainer/Element_Vector3/Y/LineEdit");
            Z = root.GetNode<LineEdit>("HBoxContainer/VBoxContainer/Element_Vector3/Z/LineEdit");
            RotationY = root.GetNode<LineEdit>("HBoxContainer/VBoxContainer/RotationY/LineEdit");
            SelectorButton = root.GetNode<Button>("HBoxContainer/Button");
            _iObjWithWeightRoot = iObjWithWeightRoot;
        }

        public IEnumerator<KeyValuePair<Node, IObjWithWeight>> GetIObjWithWeightS() => _iObjWithWeightS.GetEnumerator();

        public void ShowIObjWithWeightList()
        {
            foreach (KeyValuePair<Node, IObjWithWeight> kv in _iObjWithWeightS)
            {
                _iObjWithWeightRoot.Add(kv.Value.Root);
            }

            _iObjWithWeightRoot.Creation += OnIObjWithWeightRoot_Creation;
            _iObjWithWeightRoot.Deletion += OnIObjWithWeightRoot_Deletion;
        }
        public void HideIObjWithWeightList()
        {
            _iObjWithWeightRoot.RemoveAll();

            _iObjWithWeightRoot.Creation -= OnIObjWithWeightRoot_Creation;
            _iObjWithWeightRoot.Deletion -= OnIObjWithWeightRoot_Deletion;
        }

        public void QueueFreeIObjWithWeightNodes()
        {
            foreach (KeyValuePair<Node, IObjWithWeight> kv in _iObjWithWeightS)
            {
                kv.Value.Root.QueueFree();
            }
        }

        private void OnIObjWithWeightRoot_Creation(Node node)
        {
            _iObjWithWeightS.Add(node, new(node));
        }
        private void OnIObjWithWeightRoot_Deletion(Node node)
        {
            _iObjWithWeightS.Remove(node);
        }
    }

    private struct IObjWithWeight
    {
        public readonly Node Root;
        public readonly LineEdit Name;
        public readonly LineEdit WeightOfPlacement;

        public IObjWithWeight(Node root)
        {
            Root = root;
            Name = root.GetNode<LineEdit>("Name/LineEdit");
            WeightOfPlacement = root.GetNode<LineEdit>("WeightOfPlacement/LineEdit");
        }
    }

    private UiList _placementDataRoot;
    private Label _iObjTitle;
    private UiList _iObjWithWeightRoot;

    private readonly Dictionary<Node, Extension> _nodeToExtensions = new();
    private Extension _activeExtension;

    public override void _Ready()
    {
        base._Ready();

        _placementDataRoot = GetNode<UiList>("PlacementDataList");
        _iObjTitle = GetNode<Label>("Label");
        _iObjWithWeightRoot = GetNode<UiList>("IObjWithWeightList");

        _placementDataRoot.Creation += OnPlacementDataRoot_Creation;
        _placementDataRoot.Deletion += OnPlacementDataRoot_Deletion;

        Plugin.Disabled += OnPlugin_Disabled;
    }

    private void Select(Extension extension)
    {
        _activeExtension = extension;
        _iObjTitle.Visible = true;
        _iObjWithWeightRoot.Visible = true;
        extension.ShowIObjWithWeightList();
    }
    private void Deselect(Extension extension)
    {
        _activeExtension = null;
        _iObjTitle.Visible = false;
        _iObjWithWeightRoot.Visible = false; // Hide Add/Remove
        extension.HideIObjWithWeightList();
    }

    private void OnPlacementDataRoot_Creation(Node node)
    {
        Extension extension = new(node, _iObjWithWeightRoot);
        _nodeToExtensions.Add(node, extension);

        extension.SelectorButton.Toggled += (buttonPressed) => OnSelectorButton_Toggled(extension, buttonPressed);
    }

    private void OnPlacementDataRoot_Deletion(Node node)
    {
        Extension extension = _nodeToExtensions[node];

        // Hide Root UI & Disconnect Events //
        if (extension.SelectorButton.ButtonPressed) { Deselect(extension); }

        // Free UI Nodes //
        extension.QueueFreeIObjWithWeightNodes();
        _nodeToExtensions.Remove(node);
    }

    private void OnPlugin_Disabled()
    {
        // Clean up any nodes outside of the SceneTree
        foreach (KeyValuePair<Node, Extension> kv in _nodeToExtensions)
        {
            kv.Value.QueueFreeIObjWithWeightNodes();
        }
    }

    private void OnSelectorButton_Toggled(Extension extension, bool buttonPressed)
    {
        if (buttonPressed)
        {
            if (_activeExtension != null)
            {
                _activeExtension.SelectorButton.SetPressedNoSignal(false);
                Deselect(_activeExtension);
            }
            Select(extension);
        }
        else { Deselect(extension); }
    }
}
#endif