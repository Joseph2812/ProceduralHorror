using Godot;
using System;
using Scripts.Items;

namespace Scripts.Player;

public partial class InteractionController : Node
{
    private const float ReachMinimum = 0.7f;
    private const float ReachMaximum = 1.2f;
    private const float ReachStep = 0.1f;

    private const float PGain = 150f, DGain = 25f, MaxGrabForce = 250f;
    private const float GrabDropMargin = 0.5f;
    private const float GrabDropSqrThresholdZ = (ReachMaximum + GrabDropMargin) * (ReachMaximum + GrabDropMargin);

    private const float OutlineWidth = 2f;

    private static readonly StringName s_interactName = "interact", s_grabName = "grab";
    private static readonly StringName s_extendName = "extend", s_retractName = "retract";
    private static readonly StringName s_colliderName = "collider";
    private static readonly StringName s_outlineWidthName = "outline_width";

    // Events only for interactables that require exit
    public event Action InteractableEntered;
    public event Action InteractableExited;

    private readonly PidController _pidX = new(pGain: PGain, dGain: DGain, maxResult: MaxGrabForce);
    private readonly PidController _pidY = new(pGain: PGain, dGain: DGain, maxResult: MaxGrabForce);
    private readonly PidController _pidZ = new(pGain: PGain * 3f, dGain: DGain * 1.5f, maxResult: MaxGrabForce * 3f);

    private readonly PhysicsRayQueryParameters3D _rayParams = new();

    private Inventory _inventory;
    private PhysicsDirectSpaceState3D _space;
    private IInteractable _activeInteractable;
    private RigidBody3D _activeRigidbody;
    private float _targetReach;

    private ShaderMaterial _lastOutlinedMat;
    private bool _interactQueued;
    private bool _grabQueued;

    public override void _Ready()
    {
        base._Ready();

        _inventory = CameraController.Inst.GetNode<Inventory>("Inventory");
        _space = CameraController.Inst.GetWorld3D().DirectSpaceState;       

        Console.Inst.Opened += OnConsole_Opened;
        Console.Inst.Closed += OnConsole_Closed;

        _inventory.Opened += OnInventory_Opened;
        _inventory.Closed += OnInventory_Closed;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        // Check For Raycast Collision //
        Godot.Collections.Dictionary rayResult = RaycastFromCamera();
        GodotObject colliderObj = null;

        if (rayResult.Count > 0)
        {
            colliderObj = rayResult[s_colliderName].AsGodotObject();
            if (colliderObj is Item item)
            {
                if (_lastOutlinedMat != item.Material.NextPass)
                {
                    _lastOutlinedMat?.SetShaderParameter(s_outlineWidthName, 0f);

                    _lastOutlinedMat = (ShaderMaterial)item.Material.NextPass;
                    _lastOutlinedMat.SetShaderParameter(s_outlineWidthName, OutlineWidth);
                }     
            }
            else if (_lastOutlinedMat != null)
            {
                _lastOutlinedMat.SetShaderParameter(s_outlineWidthName, 0f);
                _lastOutlinedMat = null;
            }
        }
        else if (_lastOutlinedMat != null)
        {
            _lastOutlinedMat.SetShaderParameter(s_outlineWidthName, 0f);
            _lastOutlinedMat = null;
        }

        // Try Grabbing/Interacting //
        if (_activeRigidbody != null || (_grabQueued && TryGrab(colliderObj)))
        {
            Vector3 camPos = CameraController.Inst.GlobalPosition;
            Vector3 rbPos = _activeRigidbody.GlobalPosition;
            Vector3 targetPos = camPos + (-CameraController.Inst.GlobalBasis.Z * _targetReach);

            Vector3 rbToTarget = targetPos - rbPos;
            _activeRigidbody.ApplyCentralForce
            (
                new
                (
                    _pidX.GetNextValue((float)delta, rbToTarget.X),
                    _pidY.GetNextValue((float)delta, rbToTarget.Y),
                    _pidZ.GetNextValue((float)delta, rbToTarget.Z)
                )
            );

            if ((rbPos - camPos).LengthSquared() >= GrabDropSqrThresholdZ) { ReleaseGrab(); }
        }
        else if (_interactQueued && !(TryInteract(colliderObj) || TryPickupItem(colliderObj)) && _activeInteractable != null)
        {
            _activeInteractable.Exit();
            _activeInteractable = null;

            InteractableExited?.Invoke();
        }

        _interactQueued = false;
        _grabQueued = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);
        
        if (@event.IsActionPressed(s_interactName) && _activeRigidbody == null)
        { 
            _interactQueued = true;
        }
        else if (@event.IsActionPressed(s_grabName))
        {
            if (_activeRigidbody == null) { _grabQueued = true; }
            else                          { ReleaseGrab(); }
        }
        else if (@event.IsActionPressed(s_extendName))
        {
            _targetReach += ReachStep;
            if (_targetReach > ReachMaximum) { _targetReach = ReachMaximum; }
        }
        else if (@event.IsActionPressed(s_retractName))
        {
            _targetReach -= ReachStep;
            if (_targetReach < ReachMinimum) { _targetReach = ReachMinimum; }
        }
    }

    public void AddRayExclusion(Rid rid) { _rayParams.Exclude.Add(rid); }

    private Godot.Collections.Dictionary RaycastFromCamera()
    {
        _rayParams.From = CameraController.Inst.GlobalPosition;
        _rayParams.To = CameraController.Inst.GlobalPosition + (-CameraController.Inst.GlobalTransform.Basis.Z * ReachMaximum);

        return _space.IntersectRay(_rayParams);
    }

    private void ReleaseGrab()
    {
        _activeRigidbody.RemoveCollisionExceptionWith(Player.Inst);
        _activeRigidbody.Sleeping = false;
        _activeRigidbody = null;
    }

    private bool TryPickupItem(GodotObject colliderObj)
    {
        if (colliderObj is Item item) { return _inventory.TryAddItem(item); }
        return false;
    }

    private bool TryInteract(GodotObject colliderObj)
    {
        if (colliderObj is IInteractable interactable)
        {
            if (interactable.ExitRequired)
            {
                if (_activeInteractable != null) { return false; }

                interactable.Interact();
                _activeInteractable = interactable;

                InteractableEntered?.Invoke();
            }
            else { interactable.Interact(); }

            return true;
        }
        return false;
    }

    private bool TryGrab(GodotObject colliderObj)
    {
        if (colliderObj is RigidBody3D rb)
        {
            _activeRigidbody = rb;
            _activeRigidbody.AddCollisionExceptionWith(Player.Inst);

            _pidX.Reset();
            _pidY.Reset();
            _pidZ.Reset();

            _targetReach = Mathf.Clamp(CameraController.Inst.GlobalPosition.DistanceTo(rb.GlobalPosition), ReachMinimum, ReachMaximum);

            return true;
        }
        return false;
    }

    private void Disable()
    {
        SetProcesses(false);

        _interactQueued = false;
        _grabQueued = false;
    }

    private void SetProcesses(bool state)
    {
        SetProcess(state);
        SetProcessUnhandledInput(state);
    }

    private void OnConsole_Opened() { Disable(); }
    private void OnConsole_Closed() { SetProcesses(CameraController.Inst.Current); }

    private void OnInventory_Opened()
    {
        Disable();
        if (_activeRigidbody != null) { ReleaseGrab(); }
    }
    private void OnInventory_Closed() { SetProcesses(CameraController.Inst.Current); }
}
