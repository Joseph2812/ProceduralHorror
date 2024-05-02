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

    public Item LastItem; // Cached for AnimationFinished
    public Item Item;   
    public Item NextItem;

    public EquipState EqpState;

    public ArmState(Node3D arm, Node3D armSkeletonParent, Skeleton3D armSkeleton, MeshInstance3D armMeshInst, SpringArm3D spring, Node springTarget)
    {
        Arm = arm;
        ArmSkeletonParent = armSkeletonParent;
        ArmSkeleton = armSkeleton;
        ArmMeshInst = armMeshInst;
        Spring = spring;
        SpringTarget = springTarget;
    }
}
