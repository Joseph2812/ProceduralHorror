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
    
    public event Action<Item, Arm> ItemArmChanged;

    private ArmState _stateL, _stateR;
    private Vector3 _springOffset;

    public override void _Ready()
    {
        base._Ready();

        Node3D arm = GetNode<Node3D>("Arm_L");
        ArmAnimL = arm.GetNode<AnimationPlayer>("AnimationPlayer");
        _stateL = new(arm, GetNode<SpringArm3D>("SpringArm3D_L"), "Armature_L", "Arm_Obj_L");
        
        arm = GetNode<Node3D>("Arm_R");
        ArmAnimR = arm.GetNode<AnimationPlayer>("AnimationPlayer");
        _stateR = new(arm, GetNode<SpringArm3D>("SpringArm3D_R"), "Armature_R", "Arm_Obj_R");

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
            if (_stateL.Item != item) { QueueItem(item, _stateL, _stateR); }
            return;
        }

        // Case: No Item In Hand //
        EquipGeneral(item, _stateL);

        item.Equip(Arm.Left);
        ItemArmChanged?.Invoke(item, Arm.Left);
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
            if (_stateR.Item != item) { QueueItem(item, _stateR, _stateL); }
            return;
        }

        // Case: No Item In Hand //
        EquipGeneral(item, _stateR);

        item.Equip(Arm.Right);
        ItemArmChanged?.Invoke(item, Arm.Right);
    }
    public void EquipBoth(Item item)
    {
        if (TryDelayEquip(item, _stateL, _stateR) | TryDelayEquip(item, _stateR, _stateL)) { return; }

        if (_stateL.Item == item) // Only one needs to be checked if it's two-handed
        {
            Unequip(item);
            return;
        }

        bool queue = false;
        if (_stateL.Item != null)
        {
            Unequip(_stateL.Item);
            queue = true;
        }
        if (_stateR.Item != null)
        {
            Unequip(_stateR.Item);
            queue = true;
        }
        if (queue)
        {
            QueueItem(item, _stateL, _stateR);
            return;
        }

        PrepareArm(item, _stateL, _stateL.SpringTarget, _stateL.Spring);
        PrepareArm(item, _stateR, _stateL.SpringTarget, _stateL.Spring);

        item.Reparent(_stateL.SpringTarget, false);
        item.Position = Vector3.Zero;
        item.Rotation = -_stateL.Spring.Rotation;

        _stateL.Spring.Position = _springOffset;

        item.Equip(Arm.Both);
        ItemArmChanged?.Invoke(item, Arm.Both);
    }

    /// <summary>
    /// Ensures an item is changed into the <see cref="ArmState.EquipState.Unequipping"/>, or queued to, if matching.
    /// Makes sure <see cref="Item.Unequip"/> is only called once on an <see cref="Item"/>.
    /// </summary>
    public void Unequip(Item item)
    {
        bool SetStateOrQueue(ArmState state1, ArmState state2)
        {
            switch (state1.EqpState)
            {
                case ArmState.EquipState.Equipped:
                    state1.EqpState = ArmState.EquipState.Unequipping;
                    return true;

                case ArmState.EquipState.Equipping:
                    QueueItem(item, state1, state2);
                    return false;

                default:
                    return false;
            }
        }

        bool stateSet = false;
        if (_stateL.Item == item) { stateSet |= SetStateOrQueue(_stateL, _stateR); }
        if (_stateR.Item == item) { stateSet |= SetStateOrQueue(_stateR, _stateL); }

        if (stateSet)
        {
            item.Unequip();
            ItemArmChanged?.Invoke(item, Arm.None);
        }
    }
    
    // Not used with EquipBoth()
    private void EquipGeneral(Item item, ArmState state)
    {
        PrepareArm(item, state, state.SpringTarget, state.Spring);

        item.Reparent(state.SpringTarget, false);
        item.Position = Vector3.Zero;
        item.Rotation = -state.Spring.Rotation;

        state.Spring.Position = _springOffset;
    }

    private void PrepareArm(Item item, ArmState state, Node parent, Node3D spring)
    {
        state.Item = item;
        state.EqpState = ArmState.EquipState.Equipping;

        state.Arm.Reparent(parent, false);
        state.Arm.Position = Vector3.Zero;
        state.Arm.Rotation = -spring.Rotation;
        state.Arm.Visible = true;
    }

    private void SwapItemsInHands(ArmState state1, ArmState state2)
    {
        Unequip(state2.Item);
        QueueItem(state2.Item, state1, state2);
        if (state1.Item != null)
        {
            Unequip(state1.Item);
            QueueItem(state1.Item, state2, state1);
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
                if (state2.EqpState == ArmState.EquipState.Unequipping && state2.Item == item)
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

    private void AssignNewBoxShape(ArmState state)
    {
        (BoxShape3D box, Vector3 offset) = ConvexHull.GenerateBox
        (
            GetBoneGlobalPositions(state.ArmSkeletonParent, state.ArmSkeleton),
            state.Item.MeshInstance.Transform * state.Item.MeshInstance.Mesh.GetFaces()
        );
        
        ApplyBoxShape(state, box, offset);

        //Debug.Clear();
        //Debug.CreateBox(state.Spring, Colors.Green, Vector3.Zero, box.Size);
    }
    private void AssignNewBoxShapeTwoHanded()
    {
        (BoxShape3D box, Vector3 offset) = ConvexHull.GenerateBox
        (
            GetBoneGlobalPositions(_stateL.ArmSkeletonParent, _stateL.ArmSkeleton),
            GetBoneGlobalPositions(_stateR.ArmSkeletonParent, _stateR.ArmSkeleton),
            _stateL.Item.MeshInstance.Transform * _stateL.Item.MeshInstance.Mesh.GetFaces()
        );

        ApplyBoxShape(_stateL, box, offset);
        _stateR.Arm.Position = offset;

        //Debug.Clear();
        //Debug.CreateBox(_stateL.Spring, Colors.Green, Vector3.Zero, box.Size);
    }  
    private void ApplyBoxShape(ArmState state, BoxShape3D box, Vector3 offset)
    {
        state.Spring.Shape = box;
        state.Spring.Position = _springOffset - new Vector3(-offset.X, offset.Y, -offset.Z); // Rotating offset 180 degrees (to match SpringArm3D)
        state.Arm.Position = offset;
        state.Item.Position = offset;
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
            positions[i] = (skeletonParent.Transform * skeleton.GetBoneGlobalPose(i)).Origin; // TODO: Remove skeletonParent.Transform once I make the skeleton uniform scale
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
                    state1.EqpState = ArmState.EquipState.None;
                    state1.Item = null;                

                    EquipNextItem(state1, state2, equip);
                    break;
            }
        }
        else
        {
            if (state1.EqpState == ArmState.EquipState.Unequipping)
            {
                state1.EqpState = ArmState.EquipState.None;
                state1.Item = null;     
                
                state1.Arm.Visible = false;
            }
            else
            {
                state1.EqpState = ArmState.EquipState.Equipped;

                if (state1.Item.TwoHanded) { AssignNewBoxShapeTwoHanded(); }
                else                       { AssignNewBoxShape(state1); }
            }
        }
    }

    private void EquipNextItem(ArmState state1, ArmState state2, Action<Item> equip)
    {
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

        // Applies to both OnAnimPlayer*_AnimationFinished() methods
        // Queued equip can't be triggered in the opposite hand if it's currently doing no animations. This is needed for a swap
        // In this case it should only trigger when this arm has finished unequipping (so item can transfer)
        if (_stateR.NextItem != null && _stateR.EqpState == ArmState.EquipState.None && wasUnequippingL)
        {
            EquipNextItem(_stateR, _stateL, EquipRight);
        }
    }
    private void OnAnimPlayerR_AnimationFinished(StringName animName)
    {
        bool wasUnequippingR = _stateR.EqpState == ArmState.EquipState.Unequipping;
        ProgressStates(_stateR, _stateL, EquipRight);

        if (_stateL.NextItem != null && _stateL.EqpState == ArmState.EquipState.None && wasUnequippingR)
        {
            EquipNextItem(_stateL, _stateR, EquipLeft);
        }
    }
}