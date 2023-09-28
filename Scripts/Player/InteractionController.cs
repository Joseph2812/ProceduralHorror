using Godot;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Scripts.Player;

public partial class InteractionController : CameraController
{
    private const float MinimumReach = 1f;
    private const float MaximumReach = 2f;

    private const float PGain = 150f, DGain = 25f, MaxGrabForce = 250f;
    private const int GrabHoldMilliseconds = 125;

    private static readonly StringName _interactName = "interact", _colliderName = "collider";
    private static readonly StringName _pushName = "push", _pullName = "pull";
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

    private CancellationTokenSource _holdTokenSrc;
    private bool _heldInteract; // Used to check if it was held throughout the delay time after being released
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

            // Rotate to act on local axis. Produces smoother movement (especially when changing _targetReach)
            Basis camToRbBasis = Basis.LookingAt(rbPos - camPos, Vector3.Up);
            Vector3 rotatedRbToTarget = (targetPos - rbPos) * camToRbBasis;
            _activeRigidbody.ApplyCentralForce
            (
                 _pidX.GetNextValue((float)delta, rotatedRbToTarget.X) * camToRbBasis.X +
                 _pidY.GetNextValue((float)delta, rotatedRbToTarget.Y) * camToRbBasis.Y +
                 _pidZ.GetNextValue((float)delta, rotatedRbToTarget.Z) * camToRbBasis.Z
            );

            Debug.Clear();
            Debug.CreatePoint(GetTree().Root, Colors.Purple, targetPos);
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
        
        if (@event.IsActionPressed(_interactName)) { WaitForGrab(); }
        else if (@event.IsActionReleased(_interactName))
        { 
            ReleaseGrab();
            _interactQueued = !_heldInteract;
        }
        else if (@event.IsActionPressed(_pushName))
        {
            _targetReach += 0.1f;
            if (_targetReach > MaximumReach) { _targetReach = MaximumReach; }
        }
        else if (@event.IsActionPressed(_pullName))
        {
            _targetReach -= 0.1f;
            if (_targetReach < MinimumReach) { _targetReach = MinimumReach; }
        }
    }

    protected override void OnConsole_Opened()
    {
        base.OnConsole_Opened();

        SetProcessUnhandledInput(false);
        ReleaseGrab();
    }
    protected override void OnConsole_Closed()
    {
        base.OnConsole_Closed();

        SetProcessUnhandledInput(Current);
    }

    private Godot.Collections.Dictionary RaycastFromCamera()
    {
        _rayParams.From = GlobalPosition;
        _rayParams.To = GlobalPosition + (-GlobalTransform.Basis.Z * MaximumReach);

        return _space.IntersectRay(_rayParams);
    }

    private async void WaitForGrab()
    {
        _heldInteract = false;
        _holdTokenSrc = new();
        try                           { await Task.Delay(GrabHoldMilliseconds, _holdTokenSrc.Token); }
        catch (TaskCanceledException) { return; }

        _grabQueued = true;
        _heldInteract = true;
    }
    private void ReleaseGrab()
    {
        _holdTokenSrc.Cancel();
        if (_activeRigidbody != null)
        {
            _activeRigidbody.Sleeping = false;
            _activeRigidbody = null;
        }
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
