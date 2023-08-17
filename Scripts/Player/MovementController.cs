using Godot;
using System;

namespace Scripts.Player;

public partial class MovementController : CharacterBody3D
{
    private const float Speed = 4f;
    private const float DownwardSpeed = 0f;

    private bool _active;

    public override void _Ready()
    {
        base._Ready();

        Console.Inst.AddCommand("free-cam", new(OnConsoleCmd_FreeCamera));
        Console.Inst.AddCommand("player-cam", new(OnConsoleCmd_PlayerCamera));
        Console.Inst.Opened += OnConsole_Opened;
        Console.Inst.Closed += OnConsole_Closed;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        Velocity =
        ((
            ((Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left")) * Transform.Basis.X) +
            ((Input.GetActionStrength("move_back") - Input.GetActionStrength("move_forward")) * Transform.Basis.Z)
        ).Normalized() * Speed) + (Vector3.Down * DownwardSpeed);

        MoveAndSlide();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);

        if (@event is InputEventMouseMotion mouseMotion)
        {
            RotateY(-mouseMotion.Relative.X * CameraController.MouseSensitivity);
        }
    }

    private void SetAllProcesses(bool state)
    {
        SetPhysicsProcess(state);
        SetProcessUnhandledInput(state);
    }

    private void OnConsoleCmd_FreeCamera(string[] _) { _active = false; }
    private void OnConsoleCmd_PlayerCamera(string[] _) { _active = true; }

    private void OnConsole_Opened() { SetAllProcesses(false); }
    private void OnConsole_Closed() { SetAllProcesses(_active); }
}
