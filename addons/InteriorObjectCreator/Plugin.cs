#if TOOLS
using Godot;
using System;

namespace Addons.InteriorObjectCreator;

[Tool]
public partial class Plugin : EditorPlugin
{
    private const string DockPath = "addons/InteriorObjectCreator/Dock/Dock.tscn";

    private Control _dock;

	public override void _EnterTree()
	{
        _dock = (Control)GD.Load<PackedScene>(DockPath).Instantiate();
        AddControlToDock(DockSlot.LeftUr, _dock);
    }

	public override void _ExitTree()
	{
        RemoveControlFromDocks(_dock);
        _dock.Free();
    }
}
#endif
