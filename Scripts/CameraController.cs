using Godot;
using System;

namespace Scripts;

public partial class CameraController : Camera3D
{
    private const float Speed = 0.1f;
    private const float MouseSensitivity = 0.02f;

    private Viewport _viewport;

    public override void _Ready()
    {
        base._Ready();

        _viewport = GetViewport();

        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        Vector3 input = new Vector3
        (
            Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left"),
            Input.GetActionStrength("move_up") - Input.GetActionStrength("move_down"),
            Input.GetActionStrength("move_back") - Input.GetActionStrength("move_forward")
        ).Normalized();

        Translate(input * Speed);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);

        if (@event is InputEventMouseMotion mouseMotion)
        {
            RotateObjectLocal(Vector3.Right, -mouseMotion.Relative.Y * MouseSensitivity);
            RotateY(-mouseMotion.Relative.X * MouseSensitivity);
        }
    }
}
