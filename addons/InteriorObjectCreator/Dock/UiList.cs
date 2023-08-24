#if TOOLS
using Godot;
using System.Collections.Generic;

namespace Addons.InteriorObjectCreator;

[Tool]
public partial class UiList : VBoxContainer
{
    [Export] private PackedScene _elementUi;

    private HBoxContainer _hBox;
    private Button _removeButton;

    private readonly Stack<Node> _elements = new Stack<Node>();

    public override void _Ready()
    {
        base._Ready();

        _hBox = new HBoxContainer();
        _hBox.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        AddChild(_hBox, false, InternalMode.Back);

        Button addButton = new Button();
        _removeButton = new Button();

        addButton.Text = "Add";
        addButton.Pressed += OnAddButton_Pressed;
        
        _removeButton.Text = "Remove";
        _removeButton.Disabled = true;
        _removeButton.Pressed += OnRemoveButton_Pressed;

        _hBox.AddChild(addButton, false, InternalMode.Back);
        _hBox.AddChild(_removeButton,false, InternalMode.Back);
    }

    private void OnAddButton_Pressed()
    {
        Node element = _elementUi.Instantiate();
        
        _elements.Push(element);
        AddChild(element, false, InternalMode.Back);

        _hBox.MoveToFront();
        _removeButton.Disabled = false;
    }
    private void OnRemoveButton_Pressed()
    {
        Node element = _elements.Pop();
        element.QueueFree();

        if (_elements.Count == 0) { _removeButton.Disabled = true; }
    }
}
#endif