using Godot;
using System;

namespace Scripts.Player;

public partial class MovementController : CharacterBody3D
{
    private const float CrouchSpeedMultiplier = 0.5f;
    private const float WalkSpeed = 4f;
    private const float MaxSprintSpeed = WalkSpeed * 2f;
    private const float DownwardSpeed = 0f;
    private const float ToggleCrouchSpeed = 1.5f;

    private const float CameraStandY = 1.8f;
    private const float CameraCrouchY = 0.8f;
    private const float ColliderStandY = 0.95f;
    private const float ColliderCrouchY = ColliderStandY * 0.5f;

    private const float StaminaMaxTime = 15f;
    private const float StaminaTireThreshold = 3f;
    private const float StaminaGradient = (MaxSprintSpeed - WalkSpeed) / StaminaTireThreshold;
    private const float StaminaRefillRate = 0.5f;

    private static readonly StringName _moveLeftName = "move_left", _moveRightName = "move_right", _moveForwardName = "move_forward", _moveBackName = "move_back";
    private static readonly StringName _lookLeftName = "look_left", _lookRightName = "look_right";
    private static readonly StringName _sprintName = "sprint";
    private static readonly StringName _crouchName = "crouch";
    private static readonly NodePath _positionYPath = "position:y";

    private float _staminaTimeLeft = StaminaMaxTime;
    private bool _sprintHeld;

    private Node3D _camNode;
    private CollisionShape3D _colShape;
    private CapsuleShape3D _capShape;

    private Callable _setCollisionShapeYCall;
    private Tween _crouchCamTween, _crouchColTween;
    private bool _crouching;

    private bool _active = true;
    private bool _controlled = true;

    public override void _Ready()
    {
        base._Ready();

        _camNode = GetNode<Node3D>("Camera3D");
        _colShape = GetNode<CollisionShape3D>("CollisionShape3D");
        _capShape = (CapsuleShape3D)_colShape.Shape;

        _setCollisionShapeYCall = Callable.From<float>(SetCollisionShapeY);
        _crouchCamTween = CreateTween();
        _crouchColTween = CreateTween();

        _crouchCamTween.Kill();
        _crouchColTween.Kill();

        Console.Inst.AddCommand("free-cam", new(OnConsoleCmd_FreeCamera));
        Console.Inst.AddCommand("player-cam", new(OnConsoleCmd_PlayerCamera));
        Console.Inst.Opened += OnConsole_Opened;
        Console.Inst.Closed += OnConsole_Closed;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        RotateY(Input.GetAxis(_lookRightName, _lookLeftName) * CameraController.JoystickSensitivity * (float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        Vector3 input = GetGlobalMoveInput();
        bool sprinting = _sprintHeld && Transform.Basis.Z.Dot(-input) >= (1f / Mathf.Sqrt2) - Mathf.Epsilon;

        Velocity = Vector3.Down * DownwardSpeed;
        if (sprinting)
        {
            _staminaTimeLeft -= (float)delta;
            if (_staminaTimeLeft < 0f)
            {
                _staminaTimeLeft = 0f;
                _sprintHeld = false;
            }

            if (_active)
            {
                float sprintSpeed = GetSprintSpeed();
                Velocity += input * (_crouching ? sprintSpeed * CrouchSpeedMultiplier : sprintSpeed);
            }
        }
        else
        {
            _staminaTimeLeft += (float)delta * StaminaRefillRate;
            if (_staminaTimeLeft > StaminaMaxTime) { _staminaTimeLeft = StaminaMaxTime; }

            if (_active) { Velocity += input * (_crouching ? WalkSpeed * CrouchSpeedMultiplier : WalkSpeed); }
        }
        MoveAndSlide();

        // FOR DEBUGGING
        GetNode<Label>("/root/Main/UI/DebugLabel").Text = $"Input: {input}, Velocity: {Velocity}, Sprinting: {sprinting}, Stamina: {_staminaTimeLeft}";        
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
        else if (@event.IsActionPressed(_crouchName)) { ToggleCrouch(); }
    }

    private Vector3 GetGlobalMoveInput()
    {
        return
        (
            (Input.GetAxis(_moveLeftName, _moveRightName) * Transform.Basis.X) +
            (Input.GetAxis(_moveForwardName, _moveBackName) * Transform.Basis.Z)
        ).LimitLength();
    }

    private float GetSprintSpeed() => (_staminaTimeLeft > StaminaTireThreshold) ? MaxSprintSpeed : (StaminaGradient * _staminaTimeLeft) + WalkSpeed;

    private void SetCollisionShapeY(float posY)
    {
        _colShape.Position = new(_colShape.Position.X, posY, _colShape.Position.Z);
        _capShape.Height = posY * 2f;
    }

    private void ToggleCrouch()
    {
        _crouching = !_crouching;

        Tween.TransitionType transType;
        Tween.EaseType easeType;
        float camTargetY;
        float colTargetY;

        if (_crouching)
        {
            transType = Tween.TransitionType.Back;
            easeType = Tween.EaseType.Out;
            camTargetY = CameraCrouchY;
            colTargetY = ColliderCrouchY;
        }
        else
        {
            transType = Tween.TransitionType.Cubic;
            easeType = Tween.EaseType.InOut;
            camTargetY = CameraStandY;
            colTargetY = ColliderStandY;
        }

        _crouchCamTween.Kill();
        _crouchColTween.Kill();
        _crouchCamTween = CreateTween();
        _crouchColTween = CreateTween();

        _crouchCamTween.SetTrans(transType).SetEase(easeType);
        _crouchColTween.SetTrans(transType).SetEase(easeType);
        _crouchColTween.SetProcessMode(Tween.TweenProcessMode.Physics);

        _crouchCamTween.TweenProperty(_camNode, _positionYPath, camTargetY, GetCrouchTime(camTargetY, _camNode.Position.Y));
        _crouchColTween.TweenMethod(_setCollisionShapeYCall, _colShape.Position.Y, colTargetY, GetCrouchTime(colTargetY, _colShape.Position.Y));
    }

    private float GetCrouchTime(float target, float current) => Mathf.Abs(target - current) * (1f / ToggleCrouchSpeed);

    private void SetProcesses(bool state)
    {
        SetProcess(state);
        SetProcessUnhandledInput(state);
    }

    private void OnConsoleCmd_FreeCamera(string[] _) { _controlled = false; }
    private void OnConsoleCmd_PlayerCamera(string[] _) { _controlled = true; }

    private void OnConsole_Opened()
    {
        _active = false;
        SetProcesses(false);

        _sprintHeld = false;
    }
    private void OnConsole_Closed()
    {
        _active = _controlled;
        SetProcesses(_controlled);
    }
}
