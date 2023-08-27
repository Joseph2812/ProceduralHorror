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

        private readonly List<IObjWithWeight> _iObjWithWeightS = new();
        private readonly UiList _iObjWithWeightRoot;
        private bool _ignoreTree;

        public Extension(Node root, UiList iObjWithWeightRoot)
        {
            X = root.GetNode<LineEdit>("HBoxContainer/VBoxContainer/Element_Vector3/X/LineEdit");
            Y = root.GetNode<LineEdit>("HBoxContainer/VBoxContainer/Element_Vector3/Y/LineEdit");
            Z = root.GetNode<LineEdit>("HBoxContainer/VBoxContainer/Element_Vector3/Z/LineEdit");
            RotationY = root.GetNode<LineEdit>("HBoxContainer/VBoxContainer/RotationY/LineEdit");
            SelectorButton = root.GetNode<Button>("HBoxContainer/Button");
            _iObjWithWeightRoot = iObjWithWeightRoot;
        }

        public IEnumerator<IObjWithWeight> GetIObjWithWeightS() => _iObjWithWeightS.GetEnumerator();

        public void ShowIObjWithWeightList()
        {
            _ignoreTree = true;
            foreach (IObjWithWeight iObjWithWeight in _iObjWithWeightS)
            {
                _iObjWithWeightRoot.Add(iObjWithWeight.Root);
            }
            _ignoreTree = false;

            _iObjWithWeightRoot.ChildEnteredTree += OnIObjWithWeightRoot_ChildEnteredTree;
            _iObjWithWeightRoot.ChildExitingTree += OnIObjWithWeightRoot_ChildExitingTree;
        }
        public void HideIObjWithWeightList()
        {
            _iObjWithWeightRoot.ChildEnteredTree -= OnIObjWithWeightRoot_ChildEnteredTree;
            _iObjWithWeightRoot.ChildExitingTree -= OnIObjWithWeightRoot_ChildExitingTree;

            _iObjWithWeightRoot.RemoveAll();
        }

        private void OnIObjWithWeightRoot_ChildEnteredTree(Node node)
        {
            if (_ignoreTree || node.Name == UiList.HBoxName) { return; }

            _iObjWithWeightS.Add(new(node));
        }
        private void OnIObjWithWeightRoot_ChildExitingTree(Node node)
        {
            if (_ignoreTree || node.Name == UiList.HBoxName) { return; }

            _iObjWithWeightS.RemoveAt(node.GetIndex(true));
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

    private Node _placementDataRoot;
    private Label _iObjTitle;
    private UiList _iObjWithWeightRoot;

    private readonly List<Extension> _extensions = new();
    private Extension _activeExtension;

    public override void _Ready()
    {
        base._Ready();

        _placementDataRoot = GetNode<Node>("PlacementDataList");
        _iObjTitle = GetNode<Label>("Label");
        _iObjWithWeightRoot = GetNode<UiList>("IObjWithWeightList");

        _placementDataRoot.ChildEnteredTree += OnPlacementDataRoot_ChildEnteredTree;
        _placementDataRoot.ChildExitingTree += OnPlacementDataRoot_ChildExitingTree;
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

    private void OnPlacementDataRoot_ChildEnteredTree(Node node)
    {
        if (node.Name == UiList.HBoxName) { return; } // Stop Add/Remove buttons from being included when disabling the plugin

        Extension extension = new(node, _iObjWithWeightRoot);
        _extensions.Add(extension);

        extension.SelectorButton.Toggled += (buttonPressed) => OnSelectorButton_Toggled(extension, buttonPressed);
    }

    private void OnPlacementDataRoot_ChildExitingTree(Node node)
    {
        if (node.Name == UiList.HBoxName) { return; }

        int idx = node.GetIndex(true);
        Extension extension = _extensions[idx];

        // Disconnect Events //
        if (extension.SelectorButton.ButtonPressed) { Deselect(extension); }

        // Free UI Nodes //
        IEnumerator<IObjWithWeight> enumerator = extension.GetIObjWithWeightS();
        while (enumerator.MoveNext())
        {
            IObjWithWeight obj = enumerator.Current;
            obj.Root.QueueFree();
        }
        _extensions.RemoveAt(idx);
    }

    private void OnSelectorButton_Toggled(Extension extension, bool buttonPressed)
    {
        if (buttonPressed)
        {
            if (_activeExtension != null && _activeExtension != extension)
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