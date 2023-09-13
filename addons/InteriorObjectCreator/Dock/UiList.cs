#if TOOLS
using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Addons.InteriorObjectCreator;

[GlobalClass]
[Tool]
public partial class UiList : VBoxContainer, IEnumerable<Node>
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
        addButton.Pressed += Create;
        
        _removeButton.Text = "Remove";
        _removeButton.Disabled = true;
        _removeButton.Pressed += DeleteLast;

        _hBox.AddChild(addButton, false, InternalMode.Back);
        _hBox.AddChild(_removeButton,false, InternalMode.Back);
    }

    public IEnumerator<Node> GetEnumerator() => _elements.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int Count => _elements.Count;

    /// <summary>
    /// Add pre-existing <paramref name="element"/> to the UI.
    /// </summary>
    /// <param name="triggerCreation">Use to trigger the event <see cref="Creation"/>. Use it when you need to customise a new node before adding it, and still trigger creation functionality.</param>
    public void Add(Node element, bool triggerCreation = false)
    {
        AddChild(element, false, InternalMode.Back);

        _elements.Add(element);
        _hBox.MoveToFront();

        _removeButton.Disabled = false;

        if (triggerCreation) { Creation?.Invoke(element); }
    }

    /// <summary>
    /// Instantiate new <see cref="_elementUi"/> and add to the UI.
    /// </summary>
    public void Create() { Add(_elementUi.Instantiate(), true); }

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
        foreach (Node element in _elements) { Remove(element); }
    }

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
    /// Both removes and frees the last element from the UI.
    /// </summary>
    public void DeleteLast() { Delete(_elements.Last()); }

    /// <summary>
    /// Both removes and frees all elements from the UI.
    /// </summary>
    public void DeleteAll()
    {
        foreach (Node element in _elements) { Delete(element); }
    }
}
#endif