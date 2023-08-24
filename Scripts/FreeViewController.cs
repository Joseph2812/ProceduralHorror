using Godot;
using System;

namespace Scripts;

public partial class FreeViewController : Camera3D
{
    private const float Speed = 10f;

    public override void _Ready()
    {
        base._Ready();

        SetAllProcesses(false);

        Console.Inst.AddCommand("free-cam", new(OnConsoleCmd_FreeCamera, "Switch to free-view camera."));
        Console.Inst.AddCommand("player-cam", new(OnConsoleCmd_PlayerCamera));
        Console.Inst.Opened += OnConsole_Opened;
        Console.Inst.Closed += OnConsole_Closed;
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

        TranslateObjectLocal(input * Speed * (float)delta);
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

    private void SetAllProcesses(bool state)
    {
        SetProcess(state);
        SetProcessUnhandledInput(state);
    }

    private void OnConsoleCmd_FreeCamera(string[] _)
    {
        Current = true;
        GlobalPosition = Player.CameraController.Inst.GlobalPosition;
        GlobalRotation = Player.CameraController.Inst.GlobalRotation;

        Console.Inst.AppendLine("Switched to free-camera.");
    }
    private void OnConsoleCmd_PlayerCamera(string[] _) { Current = false; }

    private void OnConsole_Opened() { SetAllProcesses(false); }
    private void OnConsole_Closed() { SetAllProcesses(Current); }
}
