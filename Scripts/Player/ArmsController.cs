using Godot;
using System;
using Scripts.Extensions;

namespace Scripts.Player;

public partial class ArmsController : Node
{
    private const float WeightToCurrent = 0.15f; // Weighting towards current value [0, 1]
    private const float MaximumDeltaRotation = Mathf.Pi * 0.075f;

    private Node3D _nodeToRotate;
    private Node3D _parentNode;
    private Vector3 _lastParentGlobalRotation;
    private Vector3 _lastInterpDeltaRotation;

    public override void _Ready()
    {
        base._Ready();

        _nodeToRotate = GetParent<Node3D>();
        _parentNode = _nodeToRotate.GetParent<Node3D>();
        _lastParentGlobalRotation = _parentNode.GlobalRotation;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        Vector3 parentGlobalRotation = _parentNode.GlobalRotation;
        Vector3 deltaAngle = (parentGlobalRotation - _lastParentGlobalRotation).EnsureAngles();
        Vector3 smoothedDeltaRotation = _lastInterpDeltaRotation.Lerp(deltaAngle, WeightToCurrent).Clamp
        (
            Vector3.One * -MaximumDeltaRotation,
            Vector3.One * MaximumDeltaRotation
        );

        _lastParentGlobalRotation = parentGlobalRotation;
        _lastInterpDeltaRotation = smoothedDeltaRotation;

        _nodeToRotate.Rotation = smoothedDeltaRotation;
    }
}
