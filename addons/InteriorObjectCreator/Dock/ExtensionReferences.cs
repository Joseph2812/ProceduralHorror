#if TOOLS
using Godot;
using System.Collections.Generic;

namespace Addons.InteriorObjectCreator;

[Tool]
public partial class ExtensionReferences : UiList
{
    public UiElements.Extension this[Node node]
    {
        get => _nodeToExtension[node];
    }

    private readonly Dictionary<Node, UiElements.Extension> _nodeToExtension = new();

    public override void _Ready()
    {
        base._Ready();

        Creation += OnCreation;
        Deletion += OnDeletion;
    }

    public IEnumerator<KeyValuePair<Node, UiElements.Extension>> GetNodeToExtensionS() => _nodeToExtension.GetEnumerator();

    private void OnCreation(Node node) { _nodeToExtension.Add(node, new(node)); }
    private void OnDeletion(Node node) { _nodeToExtension.Remove(node); }
}
#endif