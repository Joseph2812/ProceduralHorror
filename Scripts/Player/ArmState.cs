using Godot;
using Scripts.Items;
using System;

namespace Scripts.Player;

public class ArmState
{
    public enum EquipState
    {
        None,
        Equipping,
        Equipped,
        Unequipping
    }

    public readonly Node3D Arm;
    public readonly Node3D ArmSkeletonParent;
    public readonly Skeleton3D ArmSkeleton;
    public readonly MeshInstance3D ArmMeshInst;
    public readonly SpringArm3D Spring;
    public readonly Node SpringTarget;

    public Item Item;
    public Item NextItem;
    public EquipState EqpState;

    public ArmState(Node3D arm, SpringArm3D spring, string armSkeletonParentName, string armMeshInstName)
    {
        Node3D armSkeletonParent = arm.GetNode<Node3D>(armSkeletonParentName);
        Skeleton3D armSkeleton = armSkeletonParent.GetNode<Skeleton3D>("Skeleton3D");

        Arm = arm;
        ArmSkeletonParent = armSkeletonParent;
        ArmSkeleton = armSkeleton;
        ArmMeshInst = armSkeleton.GetNode<MeshInstance3D>(armMeshInstName);
        Spring = spring;
        SpringTarget = spring.GetNode("Target");
    }
}
