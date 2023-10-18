using Godot;
using System;

namespace Scripts.Player;

public partial class InteractionController : CameraController
{
    private const float ReachMinimum = 1f;
    private const float ReachMaximum = 2f;
    private const float ReachSensitivity = 0.1f;

    private const float PGain = 150f, DGain = 25f, MaxGrabForce = 250f;
    private const float GrabDropSqrThresholdZ = (ReachMaximum + 0.5f) * (ReachMaximum + 0.5f);

    private static readonly StringName _interactName = "interact", _grabName = "grab";
    private static readonly StringName _extendName = "extend", _retractName = "retract";
    private static readonly StringName _colliderName = "collider";
    private static readonly PhysicsRayQueryParameters3D _rayParams = new();

    // Events run only for interactables that require exit
    public event Action EnteredInteractable;
    public event Action ExitedInteractable;

    private readonly PidController _pidX = new(pGain: PGain, dGain: DGain, maxResult: MaxGrabForce);
    private readonly PidController _pidY = new(pGain: PGain, dGain: DGain, maxResult: MaxGrabForce);
    private readonly PidController _pidZ = new(pGain: PGain * 3f, dGain: DGain * 1.5f, maxResult: MaxGrabForce * 3f);

    private PhysicsDirectSpaceState3D _space;
    private IInteractable _activeInteractable;
    private RigidBody3D _activeRigidbody;
    private float _targetReach;

    private bool _interactQueued;
    private bool _grabQueued;

    public override void _Ready()
    {
        base._Ready();

        _rayParams.Exclude = new() { Player.Inst.Rid };
        _space = GetWorld3D().DirectSpaceState;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (_activeRigidbody != null || (_grabQueued && TryGrab()))
        {
            Vector3 camPos = GlobalPosition;
            Vector3 rbPos = _activeRigidbody.GlobalPosition;
            Vector3 targetPos = camPos - (GlobalTransform.Basis.Z * _targetReach);
            Vector3 camToRb = rbPos - camPos;

            // Rotate to act on local axis. Produces smoother movement (especially when changing _targetReach)
            Basis camToRbBasis = Basis.LookingAt(camToRb, Vector3.Up);
            Vector3 rotatedRbToTarget = (targetPos - rbPos) * camToRbBasis;
            _activeRigidbody.ApplyCentralForce
            (
                 _pidX.GetNextValue((float)delta, rotatedRbToTarget.X) * camToRbBasis.X +
                 _pidY.GetNextValue((float)delta, rotatedRbToTarget.Y) * camToRbBasis.Y +
                 _pidZ.GetNextValue((float)delta, rotatedRbToTarget.Z) * camToRbBasis.Z
            );

            if (camToRb.LengthSquared() >= GrabDropSqrThresholdZ)
            {
                ReleaseGrab();
                return;
            }
        }
        else if (_interactQueued && !TryInteract() && _activeInteractable != null)
        {
            _activeInteractable.Exit();
            _activeInteractable = null;

            ExitedInteractable?.Invoke();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);
        
        if (@event.IsActionPressed(_interactName) && _activeRigidbody == null)
        { 
            _interactQueued = true;
        }
        else if (@event.IsActionPressed(_grabName))
        {
            if (_activeRigidbody == null) { _grabQueued = true; }
            else                          { ReleaseGrab(); }
        }
        else if (@event.IsActionPressed(_extendName))
        {
            _targetReach += ReachSensitivity;
            if (_targetReach > ReachMaximum) { _targetReach = ReachMaximum; }
        }
        else if (@event.IsActionPressed(_retractName))
        {
            _targetReach -= ReachSensitivity;
            if (_targetReach < ReachMinimum) { _targetReach = ReachMinimum; }
        }
    }

    protected override void OnConsole_Opened()
    {
        base.OnConsole_Opened();

        if (_activeRigidbody != null) { ReleaseGrab(); }
    }

    private Godot.Collections.Dictionary RaycastFromCamera()
    {
        _rayParams.From = GlobalPosition;
        _rayParams.To = GlobalPosition + (-GlobalTransform.Basis.Z * ReachMaximum);

        return _space.IntersectRay(_rayParams);
    }

    private void ReleaseGrab()
    {
        _activeRigidbody.Sleeping = false;
        _activeRigidbody = null;
    }

    private bool TryInteract()
    {
        _interactQueued = false;

        Godot.Collections.Dictionary dict = RaycastFromCamera();
        if (dict.Count != 0 && dict[_colliderName].AsGodotObject() is IInteractable interactable)
        {
            if (interactable.ExitRequired)
            {
                if (_activeInteractable != null) { return false; }

                interactable.Interact();
                _activeInteractable = interactable;

                EnteredInteractable?.Invoke();
            }
            else { interactable.Interact(); }

            return true;
        }
        return false;
    }

    private bool TryGrab()
    {
        _grabQueued = false;

        Godot.Collections.Dictionary dict = RaycastFromCamera();
        if (dict.Count != 0 && dict[_colliderName].AsGodotObject() is RigidBody3D rb)
        {
            _activeRigidbody = rb;
            _pidX.Reset();
            _pidY.Reset();
            _pidZ.Reset();

            _targetReach = (rb.GlobalPosition - GlobalPosition).Length();

            return true;
        }
        return false;
    }
}
