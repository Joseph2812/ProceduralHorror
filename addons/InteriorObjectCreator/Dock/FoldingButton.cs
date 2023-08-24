#if TOOLS
using Godot;
using System;

namespace Addons.InteriorObjectCreator;

[Tool]
public partial class FoldingButton : Button
{
    private const string RightArrow = "⇒ ";
    private const string DownArrow = "⇓ ";

    [Export] private CanvasItem _itemToFold;

    public override void _Ready()
    {
        base._Ready();

        OnPressed();
        Pressed += OnPressed;
    }

    private void OnPressed()
    {
        Text = (ButtonPressed ? DownArrow : RightArrow) + _itemToFold.Name;
        _itemToFold.Visible = ButtonPressed;
    }
}
#endif
