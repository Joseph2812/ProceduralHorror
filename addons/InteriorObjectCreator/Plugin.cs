#if TOOLS
using Godot;
using System;

namespace Addons.InteriorObjectCreator;

[Tool]
public partial class Plugin : EditorPlugin
{
    private const string DockPath = "addons/InteriorObjectCreator/Dock/Dock.tscn";

    private EditorDock _dock;

	public override void _EnterTree()
	{
        _dock = new() { DefaultSlot = EditorDock.DockSlot.LeftUr };
        _dock.AddChild(GD.Load<PackedScene>(DockPath).Instantiate());
        AddDock(_dock);
    }

	public override void _ExitTree()
	{
        RemoveDock(_dock);

        _dock.QueueFree();
        _dock = null;
    }
}
#endif
