using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Addons.InteriorObjectCreator;

[Tool]
[GlobalClass]
public partial class UiList : VBoxContainer
{
    public event Action<Node> Creation;
    public event Action<Node> Deletion;

    [Export] private PackedScene _elementUi;

    private HBoxContainer _hBox;
    private Button _removeButton;

    private readonly HashSet<Node> _elements = new();

    public override void _Ready()
    {
        base._Ready();

        _hBox = new() { Name = "ListButtons" };
        _hBox.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        AddChild(_hBox, false, InternalMode.Back);

        Button addButton = new() { Name = "Add" };
        _removeButton = new() { Name = "Remove" };

        addButton.Text = "Add";
        addButton.Pressed += Add;
        
        _removeButton.Text = "Remove";
        _removeButton.Disabled = true;
        _removeButton.Pressed += Delete;

        _hBox.AddChild(addButton, false, InternalMode.Back);
        _hBox.AddChild(_removeButton,false, InternalMode.Back);
    }

    /// <summary>
    /// Instantiate new <see cref="_elementUi"/> and add to the UI.
    /// </summary>
    public void Add()
    {
        Node element = _elementUi.Instantiate();

        Add(element);
        Creation?.Invoke(element);
    }
    ///
    /// <summary>
    /// Add pre-existing <paramref name="element"/> to the UI.
    /// </summary>
    public void Add(Node element)
    {
        AddChild(element, false, InternalMode.Back);

        _elements.Add(element);
        _hBox.MoveToFront();

        _removeButton.Disabled = false;
    }

    /// <summary>
    /// Remove <paramref name="element"/> from the UI.<para/>
    /// Note: This will NOT free the <paramref name="element"/>
    /// </summary>
    public void Remove(Node element)
    {
        RemoveChild(element);

        _elements.Remove(element);
        if (_elements.Count == 0) { _removeButton.Disabled = true; }
    }
    /// <summary>
    /// Remove all elements from the UI.<para/>
    /// Note: This will NOT free these elements.
    /// </summary>
    public void RemoveAll()
    {
        foreach (Node element in _elements)
        {
            RemoveChild(element);
        }
        _elements.Clear();

        _removeButton.Disabled = true;
    }

    /// <summary>
    /// Both removes and frees the last element in the UI.
    /// </summary>
    public void Delete() { Delete(_elements.Last()); }
    ///
    /// <summary>
    /// Both removes and frees <paramref name="element"/> from the UI.
    /// </summary>
    public void Delete(Node element)
    {
        Remove(element);
        element.QueueFree();

        Deletion?.Invoke(element);
    }

    /// <summary>
    /// Removes and deletes all elements in the UI
    /// </summary>
    public void Clear()
    {
        foreach (Node element in _elements)
        {
            element.QueueFree();
        }
        _elements.Clear();

        _removeButton.Disabled = true;
    }
}