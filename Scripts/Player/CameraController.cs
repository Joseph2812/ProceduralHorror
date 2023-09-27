using Godot;
using System;

namespace Scripts.Player;

public partial class CameraController : Camera3D
{
    public const float MouseSensitivity = 0.02f;
    public const float JoystickSensitivity = 5f;

    private const float HalfPi = Mathf.Pi * 0.5f;

    public static CameraController Inst { get; private set; }

    private static readonly StringName _lookUpName = "look_up", _lookDownName = "look_down";

    public CameraController() { Inst = this; }

    public override void _Ready()
    {
        base._Ready();

        Console.Inst.AddCommand("free-cam", new(OnConsoleCmd_FreeCamera));
        Console.Inst.AddCommand("player-cam", new(OnConsoleCmd_PlayerCamera, "Switch to player camera."));
        Console.Inst.Opened += OnConsole_Opened;
        Console.Inst.Closed += OnConsole_Closed;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        Rotation = GetCameraRotation(Input.GetAxis(_lookUpName, _lookDownName) * JoystickSensitivity * (float)delta);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);

        if (@event is InputEventMouseMotion mouseMotion)
        {
            Rotation = GetCameraRotation(mouseMotion.Relative.Y * MouseSensitivity);
        }
    }

    protected virtual void OnConsole_Opened() { SetProcesses(false); }
    protected virtual void OnConsole_Closed() { SetProcesses(Current); }

    private void SetProcesses(bool state)
    {
        SetProcess(state);
        SetProcessUnhandledInput(state);
    }

    private Vector3 GetCameraRotation(float moveY) => new(Mathf.Clamp(Rotation.X - moveY, -HalfPi, HalfPi), Rotation.Y, Rotation.Z);

    private void OnConsoleCmd_FreeCamera(string[] _) { Current = false; }
    private void OnConsoleCmd_PlayerCamera(string[] _)
    {
        Current = true;
        Console.Inst.AppendLine("Switched to player-camera.");
    }
}
