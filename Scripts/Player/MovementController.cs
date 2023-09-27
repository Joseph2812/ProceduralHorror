using Godot;
using System;

namespace Scripts.Player;

public partial class MovementController : CharacterBody3D
{
    private const float SwayIntensity = 0.025f;
    private const float SwaySpeed = 4f;
    private const float BobIntensity = 0.075f;
    private const float BobSpeed = 3f;

    private const float CrouchSpeedMultiplier = 0.5f;
    private const float WalkSpeed = 3f;
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
    private static readonly NodePath _positionYPath = "position:y", _rotationZPath = "rotation:z";

    private float _staminaTimeLeft = StaminaMaxTime;
    private bool _sprintHeld;

    private Node3D _camNode;
    private CollisionShape3D _colShape;
    private CapsuleShape3D _capShape;

    private Tween _swayToRestTween;
    private Tween _bobToRestTween;
    private float _distanceTravelled; // Used with Sin wave for swaying/bobbing

    private Callable _setCollisionShapeYCall;
    private Tween _crouchCamTween, _crouchColTween;
    private bool _crouching;

    private bool _canMove = true;
    private bool _controlled = true;

    public override void _Ready()
    {
        base._Ready();

        _camNode = GetNode<Node3D>("Camera3D");
        _colShape = GetNode<CollisionShape3D>("CollisionShape3D");
        _capShape = (CapsuleShape3D)_colShape.Shape;

        _swayToRestTween = CreateTween();
        _bobToRestTween = CreateTween();

        _setCollisionShapeYCall = Callable.From<float>(SetCollisionShapeY);
        _crouchCamTween = CreateTween();
        _crouchColTween = CreateTween();

        _swayToRestTween.Kill();
        _bobToRestTween.Kill();
        _crouchCamTween.Kill();
        _crouchColTween.Kill();

        Console.Inst.AddCommand("free-cam", new(OnConsoleCmd_FreeCamera));
        Console.Inst.AddCommand("player-cam", new(OnConsoleCmd_PlayerCamera));
        Console.Inst.Opened += OnConsole_Opened;
        Console.Inst.Closed += OnConsole_Closed;

        ((InteractionController)_camNode).EnteredInteractable += OnInteractionController_EnteredInteractable;
        ((InteractionController)_camNode).ExitedInteractable += OnInteractionController_ExitedInteractable;
    }

    private void OnInteractionController_ExitedInteractable()
    {
        _canMove = true;
        GD.Print("EXITED");
    }

    private void OnInteractionController_EnteredInteractable()
    {
        _canMove = false;
        GD.Print("ENTERED: Exitable!!!");
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        RotateY(Input.GetAxis(_lookRightName, _lookLeftName) * CameraController.JoystickSensitivity * (float)delta);
        
        if (!_canMove) { return; }

        if (_crouchCamTween.IsRunning()) { _bobToRestTween.Kill(); }
        else
        {
            _distanceTravelled += Velocity.Length() * (float)delta;
            if (_crouching) { Sway(); }
            else            { Bob(); }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        Vector3 input = GetGlobalMoveInput();
        bool sprinting = _sprintHeld && GlobalTransform.Basis.Z.Dot(-input) >= (1f / Mathf.Sqrt2) - Mathf.Epsilon;

        Velocity = Vector3.Down * DownwardSpeed;
        if (sprinting)
        {
            _staminaTimeLeft -= (float)delta;
            if (_staminaTimeLeft < 0f)
            {
                _staminaTimeLeft = 0f;
                _sprintHeld = false;
            }

            if (_canMove)
            {
                float sprintSpeed = GetSprintSpeed();
                Velocity += input * (_crouching ? sprintSpeed * CrouchSpeedMultiplier : sprintSpeed);
            }
        }
        else
        {
            _staminaTimeLeft += (float)delta * StaminaRefillRate;
            if (_staminaTimeLeft > StaminaMaxTime) { _staminaTimeLeft = StaminaMaxTime; }

            if (_canMove) { Velocity += input * (_crouching ? WalkSpeed * CrouchSpeedMultiplier : WalkSpeed); }
        }
        if (_canMove) { MoveAndSlide(); }

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

    private float GetCrouchTime(float current, float target) => Mathf.Abs(target - current) * (1f / ToggleCrouchSpeed);

    private float GetCameraSwayZ() => Mathf.Sin(_distanceTravelled * SwaySpeed) * SwayIntensity;
    private float GetDistanceFromCameraRotZ() => Mathf.Asin(_camNode.Rotation.Z * (1f / SwayIntensity)) * (1f / SwaySpeed);

    private float GetCameraBobY() => CameraStandY + (Mathf.Sin(_distanceTravelled * BobSpeed) * BobIntensity);
    private float GetDistanceFromCameraY() => Mathf.Asin((_camNode.Position.Y - CameraStandY) * (1f / BobIntensity)) * (1f / BobSpeed);

    private void SetCollisionShapeY(float posY)
    {
        _colShape.Position = new(_colShape.Position.X, posY, _colShape.Position.Z);
        _capShape.Height = posY * 2f;
    }
    
    private void SetProcesses(bool state)
    {
        SetProcess(state);
        SetProcessUnhandledInput(state);
    }

    private void Sway()
    {
        if (Velocity.IsZeroApprox())
        {
            if (_camNode.Rotation.Z != 0f && !_swayToRestTween.IsRunning())
            {
                StartSwayToRest();
                _distanceTravelled = 0f;
            }
        }
        else
        {
            if (_swayToRestTween.IsRunning())
            {
                _swayToRestTween.Kill();
                _distanceTravelled = GetDistanceFromCameraRotZ();
            }
            _camNode.Rotation = new(_camNode.Rotation.X, _camNode.Rotation.Y, GetCameraSwayZ());
        }
    }
    private void Bob()
    {
        if (Velocity.IsZeroApprox())
        {
            if (_camNode.Position.Y != CameraStandY && !_bobToRestTween.IsRunning())
            {
                _bobToRestTween.Kill();
                _bobToRestTween = CreateTween();
                _bobToRestTween.TweenProperty(_camNode, _positionYPath, CameraStandY, 0.5f);

                _distanceTravelled = 0f;
            }
        }
        else
        {
            if (_bobToRestTween.IsRunning())
            {
                _bobToRestTween.Kill();
                _distanceTravelled = GetDistanceFromCameraY();
            }
            _camNode.Position = new(_camNode.Position.X, GetCameraBobY(), _camNode.Position.Z);
        }
    }

    private void StartSwayToRest()
    {
        _swayToRestTween.Kill();
        _swayToRestTween = CreateTween();
        _swayToRestTween.TweenProperty(_camNode, _rotationZPath, 0f, 0.5f);
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

            StartSwayToRest();
        }
        _distanceTravelled = 0f;

        _crouchCamTween.Kill();
        _crouchColTween.Kill();
        _crouchCamTween = CreateTween();
        _crouchColTween = CreateTween();

        _crouchCamTween.SetTrans(transType).SetEase(easeType);
        _crouchColTween.SetTrans(transType).SetEase(easeType);
        _crouchColTween.SetProcessMode(Tween.TweenProcessMode.Physics);

        float time = GetCrouchTime(_camNode.Position.Y, camTargetY);
        _crouchCamTween.TweenProperty(_camNode, _positionYPath, camTargetY, time);
        _crouchColTween.TweenMethod(_setCollisionShapeYCall, _colShape.Position.Y, colTargetY, time);
    }

    private void OnConsoleCmd_FreeCamera(string[] _) { _controlled = false; }
    private void OnConsoleCmd_PlayerCamera(string[] _) { _controlled = true; }

    private void OnConsole_Opened()
    {
        _canMove = false;
        SetProcesses(false);

        _sprintHeld = false;
    }
    private void OnConsole_Closed()
    {
        _canMove = _controlled;
        SetProcesses(_controlled);
    }
}
