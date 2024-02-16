using Godot;
using System;

namespace Scripts;

public partial class FreeCameraController : Camera3D
{
    private const float Speed = 10f;

    private static readonly StringName s_moveLeftName = "move_left", s_moveRightName = "move_right";
    private static readonly StringName s_moveUpName = "move_up", s_moveDownName = "move_down";
    private static readonly StringName s_moveForwardName = "move_forward", s_moveBackName = "move_back";

    private static readonly StringName s_lookUpName = "look_up", s_lookDownName = "look_down";
    private static readonly StringName s_lookLeftName = "look_left", s_lookRightName = "look_right";

    public static FreeCameraController Inst { get; private set; }

    public FreeCameraController() { Inst = this; }

    public override void _Ready()
    {
        base._Ready();

        SetProcesses(false);

        Console.Inst.AddCommand("free-cam", new(OnConsoleCmd_FreeCamera, "Switch to free camera."));
        Console.Inst.AddCommand("player-cam", new(OnConsoleCmd_PlayerCamera));
        Console.Inst.Opened += OnConsole_Opened;
        Console.Inst.Closed += OnConsole_Closed;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        Vector3 input = new Vector3
        (
            Input.GetAxis(s_moveLeftName, s_moveRightName),
            Input.GetAxis(s_moveDownName, s_moveUpName),
            Input.GetAxis(s_moveForwardName, s_moveBackName)
        ).LimitLength();

        TranslateObjectLocal(input * Speed * (float)delta);
        RotateObjectLocal(Vector3.Right, Input.GetAxis(s_lookDownName, s_lookUpName) * Player.CameraController.JoystickSensitivity * (float)delta);
        RotateY(Input.GetAxis(s_lookRightName, s_lookLeftName) * Player.CameraController.JoystickSensitivity * (float)delta);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);

        if (@event is InputEventMouseMotion mouseMotion)
        {
            RotateObjectLocal(Vector3.Right, -mouseMotion.Relative.Y * Player.CameraController.MouseSensitivity);
            RotateY(-mouseMotion.Relative.X * Player.CameraController.MouseSensitivity);
        }
    }

    private void SetProcesses(bool state)
    {
        SetProcess(state);
        SetProcessUnhandledInput(state);
    }

    private void OnConsoleCmd_FreeCamera(string[] _)
    {
        Current = true;
        GlobalPosition = Player.CameraController.Inst.GlobalPosition;
        GlobalRotation = Player.CameraController.Inst.GlobalRotation;

        Console.Inst.AppendLine("Switched to free camera.");
    }
    private void OnConsoleCmd_PlayerCamera(string[] _) { Current = false; }

    private void OnConsole_Opened() { SetProcesses(false); }
    private void OnConsole_Closed() { SetProcesses(Current); }
}
