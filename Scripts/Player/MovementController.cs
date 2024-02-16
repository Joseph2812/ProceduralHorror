using Godot;
using System;

namespace Scripts.Player;

public partial class MovementController : Node
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

    private static readonly StringName s_moveLeftName = "move_left", s_moveRightName = "move_right", s_moveForwardName = "move_forward", s_moveBackName = "move_back";
    private static readonly StringName s_lookLeftName = "look_left", s_lookRightName = "look_right";
    private static readonly StringName s_sprintName = "sprint";
    private static readonly StringName s_crouchName = "crouch";
    private static readonly NodePath s_positionYPath = "position:y", s_rotationZPath = "rotation:z";

    private float _staminaTimeLeft = StaminaMaxTime;
    private bool _sprintHeld;

    private Inventory _inventory;
    private CollisionShape3D _colShape;
    private CapsuleShape3D _capShape;

    private Tween _swayToRestTween;
    private Tween _bobToRestTween;
    private float _distanceTravelled; // Used with Sin wave for swaying/bobbing

    private Callable _setCollisionShapeYCall;
    private Tween _crouchCamTween, _crouchColTween;
    private bool _crouching;

    private bool _interacting;

    public override void _Ready()
    {
        base._Ready();
     
        _inventory = CameraController.Inst.GetNode<Inventory>("Inventory");
        _colShape = Player.Inst.GetNode<CollisionShape3D>("CollisionShape3D");
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

        Console.Inst.Opened += OnConsole_Opened;
        Console.Inst.Closed += OnConsole_Closed;

        InteractionController interactionController = CameraController.Inst.GetNode<InteractionController>("InteractionController");
        interactionController.InteractableEntered += OnInteractionController_InteractableEntered;
        interactionController.InteractableExited += OnInteractionController_InteractableExited;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        Player.Inst.RotateY(Input.GetAxis(s_lookRightName, s_lookLeftName) * CameraController.JoystickSensitivity * (float)delta);

        _distanceTravelled += Player.Inst.Velocity.Length() * (float)delta;
        if (_crouching)                        { Sway(); }
        else if (!_crouchCamTween.IsRunning()) { Bob(); }
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        Vector3 input = GetGlobalMoveInput();
        bool sprinting = _sprintHeld && Player.Inst.GlobalTransform.Basis.Z.Dot(-input) >= (1f / Mathf.Sqrt2) - Mathf.Epsilon;

        Player.Inst.Velocity = Vector3.Down * DownwardSpeed;
        if (sprinting)
        {
            _staminaTimeLeft -= (float)delta;
            if (_staminaTimeLeft < 0f)
            {
                _staminaTimeLeft = 0f;
                _sprintHeld = false;
            }

            if (CanMove())
            {
                float sprintSpeed = GetSprintSpeed();
                Player.Inst.Velocity += input * (_crouching ? sprintSpeed * CrouchSpeedMultiplier : sprintSpeed);
            }
        }
        else
        {
            _staminaTimeLeft += (float)delta * StaminaRefillRate;
            if (_staminaTimeLeft > StaminaMaxTime) { _staminaTimeLeft = StaminaMaxTime; }

            if (CanMove()) { Player.Inst.Velocity += input * (_crouching ? WalkSpeed * CrouchSpeedMultiplier : WalkSpeed); }
        }
        Player.Inst.MoveAndSlide();

        // FOR DEBUGGING
        GetNode<Label>("/root/Main/UI/DebugLabel").Text = $"Input: {input}, Velocity: {Player.Inst.Velocity}, Sprinting: {sprinting}, Stamina: {_staminaTimeLeft}";        
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);

        if (@event is InputEventMouseMotion mouseMotion)
        {
            Player.Inst.RotateY(-mouseMotion.Relative.X * CameraController.MouseSensitivity);
        }
        else if (@event.IsActionPressed(s_sprintName)) { _sprintHeld = true; }
        else if (@event.IsActionReleased(s_sprintName)) { _sprintHeld = false; }
        else if (@event.IsActionPressed(s_crouchName)) { ToggleCrouch(); }
    }

    private Vector3 GetGlobalMoveInput()
    {
        return
        (
            (Input.GetAxis(s_moveLeftName, s_moveRightName) * Player.Inst.Transform.Basis.X) +
            (Input.GetAxis(s_moveForwardName, s_moveBackName) * Player.Inst.Transform.Basis.Z)
        ).LimitLength();
    }

    private bool CanMove() => CameraController.Inst.Current && !Console.Inst.IsOpen && !_interacting && !_inventory.Visible;

    private float GetSprintSpeed() => (_staminaTimeLeft > StaminaTireThreshold) ? MaxSprintSpeed : (StaminaGradient * _staminaTimeLeft) + WalkSpeed;

    private float GetCrouchTime(float current, float target) => Mathf.Abs(target - current) * (1f / ToggleCrouchSpeed);

    private float GetCameraSwayZ() => Mathf.Sin(_distanceTravelled * SwaySpeed) * SwayIntensity;
    private float GetDistanceFromCameraRotZ() => Mathf.Asin(CameraController.Inst.Rotation.Z * (1f / SwayIntensity)) * (1f / SwaySpeed);

    private float GetCameraBobY() => CameraStandY + (Mathf.Sin(_distanceTravelled * BobSpeed) * BobIntensity);
    private float GetDistanceFromCameraY() => Mathf.Asin((CameraController.Inst.Position.Y - CameraStandY) * (1f / BobIntensity)) * (1f / BobSpeed);

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
        if (Player.Inst.Velocity.IsZeroApprox())
        {
            if (CameraController.Inst.Rotation.Z != 0f && !_swayToRestTween.IsRunning())
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
            CameraController.Inst.Rotation = new(CameraController.Inst.Rotation.X, CameraController.Inst.Rotation.Y, GetCameraSwayZ());
        }
    }
    private void Bob()
    {
        if (Player.Inst.Velocity.IsZeroApprox())
        {
            if (CameraController.Inst.Position.Y != CameraStandY && !_bobToRestTween.IsRunning())
            {
                _bobToRestTween.Kill();
                _bobToRestTween = CreateTween();
                _bobToRestTween.TweenProperty(CameraController.Inst, s_positionYPath, CameraStandY, 0.5f);

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
            CameraController.Inst.Position = new(CameraController.Inst.Position.X, GetCameraBobY(), CameraController.Inst.Position.Z);
        }
    }

    private void StartSwayToRest()
    {
        _swayToRestTween.Kill();
        _swayToRestTween = CreateTween();
        _swayToRestTween.TweenProperty(CameraController.Inst, s_rotationZPath, 0f, 0.5f);
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

            _bobToRestTween.Kill();
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

        float time = GetCrouchTime(CameraController.Inst.Position.Y, camTargetY);
        _crouchCamTween.TweenProperty(CameraController.Inst, s_positionYPath, camTargetY, time);
        _crouchColTween.TweenMethod(_setCollisionShapeYCall, _colShape.Position.Y, colTargetY, time);
    }

    private void OnConsole_Opened()
    {
        SetProcesses(false); // Turns off all controls
        _sprintHeld = false; // When input handling is off, turn off held button
    }
    private void OnConsole_Closed() { SetProcesses(CameraController.Inst.Current); }

    private void OnInteractionController_InteractableEntered() { _interacting = true; }
    private void OnInteractionController_InteractableExited() { _interacting = false; }
}
