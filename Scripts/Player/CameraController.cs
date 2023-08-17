using Godot;
using System;

namespace Scripts.Player;

public partial class CameraController : Camera3D
{
    public const float MouseSensitivity = 0.02f;

    public static CameraController Inst { get; private set; }

    public CameraController() { Inst = this; }

    public override void _Ready()
    {
        base._Ready();

        Console.Inst.AddCommand("free-cam", new(OnConsoleCmd_FreeCamera));
        Console.Inst.AddCommand("player-cam", new(OnConsoleCmd_PlayerCamera, "Switch to player camera."));
        Console.Inst.Opened += OnConsole_Opened;
        Console.Inst.Closed += OnConsole_Closed;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);

        if (@event is InputEventMouseMotion mouseMotion)
        {
            const float HalfPi = Mathf.Pi * 0.5f;
            Rotation = Vector3.Right * Mathf.Clamp(Rotation.X - (mouseMotion.Relative.Y * MouseSensitivity), -HalfPi, HalfPi);
        }
    }

    private void OnConsoleCmd_FreeCamera(string[] _) { Current = false; }
    private void OnConsoleCmd_PlayerCamera(string[] _)
    {
        Current = true;
        Console.Inst.AppendLine("Switched to player-camera.");
    }

    private void OnConsole_Opened() { SetProcessUnhandledInput(false); }
    private void OnConsole_Closed() { SetProcessUnhandledInput(Current); }
}
