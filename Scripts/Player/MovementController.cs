using Godot;
using System;

namespace Scripts.Player;

public partial class MovementController : CharacterBody3D
{
    private const float WalkSpeed = 4f;
    private const float MaxSprintSpeed = WalkSpeed * 2f;
    private const float DownwardSpeed = 0f;

    private const float StaminaMaxTime = 15f;
    private const float StaminaTireThreshold = 3f;
    private const float StaminaGradient = (MaxSprintSpeed - WalkSpeed) / StaminaTireThreshold;
    private const float StaminaRefillRate = 0.5f;

    private static readonly StringName _moveLeftName = "move_left", _moveRightName = "move_right", _moveForwardName = "move_forward", _moveBackName = "move_back";
    private static readonly StringName _sprintName = "sprint";

    private float _staminaTimeLeft = StaminaMaxTime;
    private bool _sprintHeld;

    private bool _active = true;
    private bool _controlled = true; 

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

        Vector3 dir = GetGlobalMoveDirection();
        bool sprinting = _sprintHeld && Transform.Basis.Z.Dot(-dir) >= 0.5f;

        Velocity = Vector3.Down * DownwardSpeed;
        if (sprinting)
        {
            _staminaTimeLeft -= (float)delta;
            if (_staminaTimeLeft < 0f) { _staminaTimeLeft = 0f; }

            if (_active)
            {
                float sprintSpeed = GetSprintSpeed();
                if (sprintSpeed == WalkSpeed) { _sprintHeld = false; }

                Velocity += dir * sprintSpeed;
            }
        }
        else
        {
            _staminaTimeLeft += (float)delta * StaminaRefillRate;
            if (_staminaTimeLeft > StaminaMaxTime) { _staminaTimeLeft = StaminaMaxTime; }

            if (_active) { Velocity += dir * WalkSpeed; }
        }
        MoveAndSlide();

        // FOR DEBUGGING
        //GetNode<Label>("/root/Main/UI/DebugLabel").Text = $"Velocity: {Velocity}, Sprinting: {sprinting}, Stamina: {_staminaTimeLeft}";        
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);

        if (@event is InputEventMouseMotion mouseMotion)
        {
            RotateY(-mouseMotion.Relative.X * CameraController.MouseSensitivity);
        }
        else if (@event.IsActionPressed(_sprintName)) { _sprintHeld = true; }
        else if (@event.IsActionReleased(_sprintName)) { _sprintHeld = false; }
    }

    private Vector3 GetGlobalMoveDirection()
    {
        return
        (
            (Input.GetAxis(_moveLeftName, _moveRightName) * Transform.Basis.X) +
            (Input.GetAxis(_moveForwardName, _moveBackName) * Transform.Basis.Z)
        ).Normalized();
    }

    private float GetSprintSpeed() => (_staminaTimeLeft > StaminaTireThreshold) ? MaxSprintSpeed : (StaminaGradient * _staminaTimeLeft) + WalkSpeed;

    private void OnConsoleCmd_FreeCamera(string[] _) { _controlled = false; }
    private void OnConsoleCmd_PlayerCamera(string[] _) { _controlled = true; }

    private void OnConsole_Opened()
    {
        _active = false;
        SetProcessUnhandledInput(false);

        _sprintHeld = false;
    }
    private void OnConsole_Closed()
    {
        _active = _controlled;
        SetProcessUnhandledInput(_controlled);
    }
}
