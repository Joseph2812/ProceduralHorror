using Godot;
using System;
using Scripts.Items;

namespace Scripts.Player;

public partial class ArmsManager : Node3D
{
    public enum Arm
    {
        None,
        Left,
        Right,
        Both
    }

    /// <summary>
    /// <see cref="AnimationPlayer"/> for left arm.
    /// </summary>
    public static AnimationPlayer ArmAnimL { get; private set; }
    /// <summary>
    /// <see cref="AnimationPlayer"/> for right arm.
    /// </summary>
    public static AnimationPlayer ArmAnimR { get; private set; }
    
    public event Action<Item, Arm> EquippedStateChanged;

    private ArmState _stateL, _stateR;
    private Vector3 _springOffset;

    public override void _Ready()
    {
        base._Ready();

        Node3D arm = GetNode<Node3D>("Arm_L");
        Node3D armSkeletonParent = arm.GetNode<Node3D>("Armature_L");
        Skeleton3D armSkeleton = armSkeletonParent.GetNode<Skeleton3D>("Skeleton3D");
        SpringArm3D spring = GetNode<SpringArm3D>("SpringArm3D_L");
        _stateL = new
        (
            arm              : arm,
            armSkeletonParent: armSkeletonParent,
            armSkeleton      : armSkeleton,
            armMeshInst      : armSkeleton.GetNode<MeshInstance3D>("Arm_Obj_L"),
            spring           : spring,
            springTarget     : spring.GetNode("Target")
        );
        ArmAnimL = arm.GetNode<AnimationPlayer>("AnimationPlayer");

        arm = GetNode<Node3D>("Arm_R");
        armSkeletonParent = arm.GetNode<Node3D>("Armature_R");
        armSkeleton = armSkeletonParent.GetNode<Skeleton3D>("Skeleton3D");
        spring = GetNode<SpringArm3D>("SpringArm3D_R");
        _stateR = new
        (
            arm              : arm,
            armSkeletonParent: armSkeletonParent,
            armSkeleton      : armSkeleton,
            armMeshInst      : armSkeleton.GetNode<MeshInstance3D>("Arm_Obj_R"),
            spring           : spring,
            springTarget     : spring.GetNode("Target")
        );
        ArmAnimR = arm.GetNode<AnimationPlayer>("AnimationPlayer");

        _springOffset = _stateL.Spring.SpringLength * Vector3.Back;

        GetParent().GetNode<Inventory>("Inventory").ItemRemoved += (item) => Unequip(item);
        ArmAnimL.AnimationFinished += OnAnimPlayerL_AnimationFinished;
        ArmAnimR.AnimationFinished += OnAnimPlayerR_AnimationFinished;
    }

    /// <summary>
    /// Sets a PhysicsBody to exclude from the collision detection, which moves the arms back.
    /// </summary>
    public void AddCollisionExclusion(Rid rid)
    {
        _stateL.Spring.AddExcludedObject(rid);
        _stateR.Spring.AddExcludedObject(rid);
    }

    // These Public Methods Should ONLY Be Called By Inventory //
    public void EquipLeft(Item item)
    {
        if (TryDelayEquip(item, _stateL, _stateR)) { return; }

        // Cases: Check For Item In Hand //       
        if (_stateR.Item == item)
        {
            SwapItemsInHands(_stateL, _stateR);
            return;
        }
        if (_stateL.Item != null)
        {
            Unequip(_stateL.Item);
            if (_stateL.LastItem != item) { QueueItem(item, _stateL, _stateR); }
            return;
        }

        // Case: No Item In Hand //
        EquipGeneral(item, _stateL);

        item.Equip(Arm.Left);
        EquippedStateChanged?.Invoke(item, Arm.Left);
    }
    public void EquipRight(Item item)
    {
        if (TryDelayEquip(item, _stateR, _stateL)) { return; }

        // Cases: Check For Item In Hand //
        if (_stateL.Item == item)
        {
            SwapItemsInHands(_stateR, _stateL);
            return;
        }
        if (_stateR.Item != null)
        {
            Unequip(_stateR.Item);
            if (_stateR.LastItem != item) { QueueItem(item, _stateR, _stateL); }
            return;
        }

        // Case: No Item In Hand //
        EquipGeneral(item, _stateR);

        item.Equip(Arm.Right);
        EquippedStateChanged?.Invoke(item, Arm.Right);
    }
    public void EquipBoth(Item item)
    {
        if (TryDelayEquip(item, _stateL, _stateR) | TryDelayEquip(item, _stateR, _stateL)) { return; }

        if (_stateL.Item == item) // Only one needs to be checked if it's two-handed
        {
            Unequip(item);
            return;
        }

        bool canQueue = false;
        if (_stateL.Item != null)
        {
            Unequip(_stateL.Item);
            canQueue = true;
        }
        if (_stateR.Item != null)
        {
            Unequip(_stateR.Item);
            canQueue = true;
        }
        if (canQueue)
        {
            QueueItem(item, _stateL, _stateR);
            return;
        }

        _stateL.Arm.Visible = true;
        _stateR.Arm.Visible = true;
        _stateL.LastItem = item;
        _stateR.LastItem = item;
        _stateL.Item = item;
        _stateR.Item = item;
        _stateL.EqpState = ArmState.EquipState.Equipping;
        _stateR.EqpState = ArmState.EquipState.Equipping;

        _stateL.Arm.Reparent(_stateL.SpringTarget, false);
        _stateR.Arm.Reparent(_stateL.SpringTarget, false);
        item.Reparent(_stateL.SpringTarget, false);

        _stateL.Arm.Position = Vector3.Zero;
        _stateL.Arm.Rotation = -_stateL.Spring.Rotation;
        _stateR.Arm.Position = Vector3.Zero;
        _stateR.Arm.Rotation = -_stateL.Spring.Rotation;

        item.Position = Vector3.Zero;
        item.Rotation = -_stateL.Spring.Rotation;
        item.IdleStarted += () => AssignNewBoxShapeTwoHanded(item);

        _stateL.Spring.Position = _springOffset;

        item.Equip(Arm.Both);
        EquippedStateChanged?.Invoke(item, Arm.Both);
    }

    /// <summary>
    /// Ensures an item is fully unequipped from <see cref="ArmsManager"/>, and <see cref="Item.Unequip"/> is only called once.
    /// </summary>
    /// <param name="item"></param>
    public void Unequip(Item item)
    {
        bool unequipped = false;
        if (_stateL.Item == item)
        {         
            if (_stateL.EqpState == ArmState.EquipState.Equipped)
            {
                _stateL.Item = null;
                _stateL.EqpState = ArmState.EquipState.Unequipping;
                unequipped = true;
            }
            else { QueueItem(item, _stateL, _stateR); }
        }
        if (_stateR.Item == item)
        {          
            if (_stateR.EqpState == ArmState.EquipState.Equipped)
            {
                _stateR.Item = null;
                _stateR.EqpState = ArmState.EquipState.Unequipping;
                unequipped = true;
            }
            else { QueueItem(item, _stateR, _stateL); }         
        }

        if (unequipped)
        {
            item.Unequip();
            EquippedStateChanged?.Invoke(item, Arm.None);
        }
    }

    private void EquipGeneral(Item item, ArmState state)
    {
        state.Arm.Visible = true;
        state.LastItem = item;
        state.Item = item;
        state.EqpState = ArmState.EquipState.Equipping;

        state.Arm.Reparent(state.SpringTarget, false);
        item.Reparent(state.SpringTarget, false);

        state.Arm.Position = Vector3.Zero;
        state.Arm.Rotation = -state.Spring.Rotation;

        item.Position = Vector3.Zero;
        item.Rotation = -state.Spring.Rotation;
        item.IdleStarted += () => AssignNewBoxShape(item, state);

        state.Spring.Position = _springOffset;
    }

    private void SwapItemsInHands(ArmState state1, ArmState state2)
    {
        Unequip(state2.Item);
        QueueItem(state2.LastItem, state1, state2);
        if (state1.Item != null)
        {
            Unequip(state1.Item);
            QueueItem(state1.LastItem, state2, state1);
        }
    }

    private bool TryDelayEquip(Item item, ArmState state1, ArmState state2)
    {
        switch (state1.EqpState)
        {
            case ArmState.EquipState.Equipping:
            case ArmState.EquipState.Unequipping:
                if (state2.EqpState == ArmState.EquipState.Equipped && item.TwoHanded)
                {
                    Unequip(state2.Item);
                }
                QueueItem(item, state1, state2);
                return true;

            default:
                if (state2.EqpState == ArmState.EquipState.Unequipping && state2.LastItem == item)
                {
                    QueueItem(item, state1, state2);
                    return true;
                }
                return false;
        }
    }

    private void QueueItem(Item item, ArmState state1, ArmState state2)
    {
        // If it's single handed, there should only be one reference to it, unless it's being used to unequip itself
        if (state2.NextItem == item && state2.EqpState != ArmState.EquipState.Equipping && !item.TwoHanded)
        {
            state2.NextItem = null;
        }
        state1.NextItem = item;
    }

    private void AssignNewBoxShape(Item item, ArmState state)
    {
        (BoxShape3D box, Vector3 offset) = ConvexHull.GenerateBox
        (
            GetBoneGlobalPositions(state.ArmSkeletonParent, state.ArmSkeleton),
            item.MeshInstance.Transform * item.MeshInstance.Mesh.GetFaces()
        );

        state.Spring.Shape = box;
        state.Spring.Position = _springOffset - new Vector3(-offset.X, offset.Y, -offset.Z); // Rotating offset 180 degrees (to match SpringArm3D)
        state.Arm.Position = offset;
        item.Position = offset;

        //Debug.Clear();
        //Debug.CreateBox(state.Spring, Colors.Green, Vector3.Zero, box.Size);
    }

    private void AssignNewBoxShapeTwoHanded(Item item)
    {
        (BoxShape3D box, Vector3 offset) = ConvexHull.GenerateBox
        (
            GetBoneGlobalPositions(_stateL.ArmSkeletonParent, _stateL.ArmSkeleton),
            GetBoneGlobalPositions(_stateR.ArmSkeletonParent, _stateR.ArmSkeleton),
            item.MeshInstance.Transform * item.MeshInstance.Mesh.GetFaces()
        );

        _stateL.Spring.Shape = box;
        _stateL.Spring.Position = _springOffset - new Vector3(-offset.X, offset.Y, -offset.Z); // Rotating offset 180 degrees (to match SpringArm3D)
        _stateL.Arm.Position = offset;
        _stateR.Arm.Position = offset;
        item.Position = offset;

        //Debug.Clear();
        //Debug.CreateBox(_stateL.Spring, Colors.Green, Vector3.Zero, box.Size);
    }

    /// <summary>
    /// Global in relation to its own skeleton origin (since there's a heirarchy of bones and therefore local positions), not global to the scene.
    /// </summary>
    private Vector3[] GetBoneGlobalPositions(Node3D skeletonParent, Skeleton3D skeleton)
    {
        int count = skeleton.GetBoneCount();
        Vector3[] positions = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            positions[i] = (skeletonParent.Transform * skeleton.GetBoneGlobalPose(i)).Origin; // TODO: Remove skeletonParent.Transform once I make the arm uniform scale
        }
        return positions;
    }

    private void ProgressStates(ArmState state1, ArmState state2, Action<Item> equip)
    {
        if (state1.NextItem != null)
        {
            switch (state1.EqpState)
            {
                case ArmState.EquipState.Equipping:
                    state1.EqpState = ArmState.EquipState.Equipped;

                    if (state1.Item.TwoHanded)
                    {
                        if (state2.EqpState != ArmState.EquipState.Equipped) { return; }

                        if (state1.NextItem == state1.Item)
                        {
                            state1.NextItem = null;
                            state2.NextItem = null;
                        }
                        Unequip(state1.Item);
                    }
                    else
                    {
                        if (state1.NextItem == state2.Item) { SwapItemsInHands(state1, state2); }
                        else
                        {
                            if (state1.NextItem == state1.Item) { state1.NextItem = null; } // Stops same item being re-equipped
                            Unequip(state1.Item);
                        }
                    }                      
                    break;

                case ArmState.EquipState.Unequipping:
                    ProgressAfterUnequipAndNextItem(state1, state2, equip);
                    break;
            }
        }
        else
        {
            if (state1.EqpState == ArmState.EquipState.Unequipping)
            {
                state1.EqpState = ArmState.EquipState.None;
                state1.Arm.Visible = false;
            }
            else { state1.EqpState = ArmState.EquipState.Equipped; }
        }
    }

    private void ProgressAfterUnequipAndNextItem(ArmState state1, ArmState state2, Action<Item> equip)
    {
        state1.EqpState = ArmState.EquipState.None;

        if (state1.NextItem.TwoHanded)
        {
            if (state2.EqpState != ArmState.EquipState.None) { return; }

            Item nextItem = state1.NextItem;

            state1.NextItem = null;
            state2.NextItem = null;
            EquipBoth(nextItem);
        }
        else
        {
            Item nextItem = state1.NextItem;

            state1.NextItem = null;
            equip(nextItem);
        }
    }

    private void OnAnimPlayerL_AnimationFinished(StringName animName)
    {
        bool wasUnequippingL = _stateL.EqpState == ArmState.EquipState.Unequipping;
        ProgressStates(_stateL, _stateR, EquipLeft);

        // Check if other arm has something queued and is state None (since without animation queued, equip can't be triggered)
        // In this case, it'll be a swap, and should only trigger when this arm is finished unequipping
        if (_stateR.NextItem != null && _stateR.EqpState == ArmState.EquipState.None && wasUnequippingL)
        {
            ProgressAfterUnequipAndNextItem(_stateR, _stateL, EquipRight);
        }
    }
    private void OnAnimPlayerR_AnimationFinished(StringName animName)
    {
        bool wasUnequippingR = _stateR.EqpState == ArmState.EquipState.Unequipping;
        ProgressStates(_stateR, _stateL, EquipRight);

        if (_stateL.NextItem != null && _stateL.EqpState == ArmState.EquipState.None && wasUnequippingR)
        {
            ProgressAfterUnequipAndNextItem(_stateL, _stateR, EquipLeft);
        }
    }
}