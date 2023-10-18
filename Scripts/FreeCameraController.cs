using Godot;
using System;

namespace Scripts;

public partial class FreeCameraController : Camera3D
{
    private const float Speed = 10f;

    private static readonly StringName _moveLeftName = "move_left", _moveRightName = "move_right";
    private static readonly StringName _moveUpName = "move_up", _moveDownName = "move_down";
    private static readonly StringName _moveForwardName = "move_forward", _moveBackName = "move_back";

    private static readonly StringName _lookUpName = "look_up", _lookDownName = "look_down";
    private static readonly StringName _lookLeftName = "look_left", _lookRightName = "look_right";

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
            Input.GetAxis(_moveLeftName, _moveRightName),
            Input.GetAxis(_moveDownName, _moveUpName),
            Input.GetAxis(_moveForwardName, _moveBackName)
        ).LimitLength();

        TranslateObjectLocal(input * Speed * (float)delta);
        RotateObjectLocal(Vector3.Right, Input.GetAxis(_lookDownName, _lookUpName) * Player.CameraController.JoystickSensitivity * (float)delta);
        RotateY(Input.GetAxis(_lookRightName, _lookLeftName) * Player.CameraController.JoystickSensitivity * (float)delta);
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
