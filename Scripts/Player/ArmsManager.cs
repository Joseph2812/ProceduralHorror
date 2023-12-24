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

    public event Action<Item, Arm> EquippedStateChanged;

    private Node3D _armL, _armR;
    private MeshInstance3D _armMeshInstL, _armMeshInstR;
    private AnimationPlayer[] _animL, _animR, _animBoth;
    private Item _itemL, _itemR;

    private SpringArm3D _springL, _springR;
    private Node _targetL, _targetR;

    public override void _Ready()
    {
        base._Ready();

        _armL = GetNode<Node3D>("Arm_L");
        _armR = GetNode<Node3D>("Arm_R");

        _armMeshInstL = _armL.GetNode<MeshInstance3D>("Arm_Obj");
        _armMeshInstR = _armR.GetNode<MeshInstance3D>("Arm_Obj");

        AnimationPlayer animL = _armL.GetNode<AnimationPlayer>("AnimationPlayer");
        AnimationPlayer animR = _armR.GetNode<AnimationPlayer>("AnimationPlayer");

        _animL = new AnimationPlayer[] { animL };
        _animR = new AnimationPlayer[] { animR };
        _animBoth = new AnimationPlayer[] { animL, animR };

        _springL = GetNode<SpringArm3D>("SpringArm3D_L");
        _springR = GetNode<SpringArm3D>("SpringArm3D_R");
        _targetL = _springL.GetNode("Target");
        _targetR = _springR.GetNode("Target");

        GetParent().GetNode<Inventory>("Inventory").ItemRemoved += (item) => Unequip(item);
    }

    /// <summary>
    /// Sets a PhysicsBody to exclude from the collision detection, which moves the arms back.
    /// </summary>
    public void AddCollisionExclusion(Rid rid)
    {
        _springL.AddExcludedObject(rid);
        _springR.AddExcludedObject(rid);
    }

    // These Public Methods Should ONLY Be Called By Inventory //
    public void EquipLeft(Item item)
    {
        if (_itemL == item)
        {
            UnequipLeftOnly();
            return;
        }
        if (_itemR == item)
        {
            UnequipRightOnly();
            if (_itemL != null)
            {
                Item itemL = _itemL;
                UnequipLeftOnly();
                EquipRight(itemL);
            }
        }
        if (_itemL != null) { Unequip(_itemL); }

        _itemL = item;
        item.StartedIdle += AssignNewBoxShapeDeferredLeft;

        _armL.Visible = true;
        _armL.Reparent(_targetL);
        item.Reparent(_targetL);
        item.Equip(_animL);        

        EquippedStateChanged?.Invoke(item, Arm.Left);
    }
    public void EquipRight(Item item)
    {
        if (_itemR == item)
        {
            UnequipRightOnly();
            return;
        }
        if (_itemL == item)
        {
            UnequipLeftOnly();
            if (_itemR != null)
            {
                Item itemR = _itemR;
                UnequipRightOnly();
                EquipLeft(itemR);
            }
        }
        if (_itemR != null) { Unequip(_itemR); }

        _itemR = item;
        item.StartedIdle += AssignNewBoxShapeDeferredRight;

        _armR.Visible = true;
        _armR.Reparent(_targetR);
        item.Reparent(_targetR);
        item.Equip(_animR);    

        EquippedStateChanged?.Invoke(item, Arm.Right);       
    }
    public void EquipBoth(Item item)
    {
        if (_itemL == item) // Only one needs to be checked if it's two-handed
        {
            UnequipBothOnly();
            return; 
        } 
        if (_itemL != null) { Unequip(_itemL); }
        if (_itemR != null) { Unequip(_itemR); }

        _itemL = item;
        _itemR = item;
        item.StartedIdle += AssignNewBoxShapeDeferredLeft;

        _armL.Visible = true;
        _armR.Visible = true;
        _armL.Reparent(_targetL);
        _armR.Reparent(_targetL);

        item.Reparent(_targetL);
        item.Equip(_animBoth);

        EquippedStateChanged?.Invoke(item, Arm.Both);  
    }

    /// <summary>
    /// Ensures an item is fully unequipped from <see cref="ArmsManager"/>, and <see cref="Item.Unequip"/> is only called once.
    /// </summary>
    /// <param name="item"></param>
    public void Unequip(Item item)
    {
        bool unequipped = false;
        if (_itemL == item)
        {
            _itemL = null;
            _armL.Visible = false;

            unequipped = true;
        }
        if (_itemR == item)
        {
            _itemR = null;
            _armR.Visible = false;

            unequipped = true;
        }

        if (unequipped)
        {
            item.Unequip();
            EquippedStateChanged?.Invoke(item, Arm.None);
        }
    }

    // Used Internally When You Have The Context (knowing it's a singular item) To Call It Once //
    private void UnequipLeftOnly()
    {
        _itemL.Unequip();
        EquippedStateChanged?.Invoke(_itemL, Arm.None);

        _armL.Visible = false;
        _itemL = null;       
    }
    private void UnequipRightOnly()
    {
        _itemR.Unequip();
        EquippedStateChanged?.Invoke(_itemR, Arm.None);

        _armR.Visible = false;
        _itemR = null;
    }
    private void UnequipBothOnly()
    {
        _itemL.Unequip();
        EquippedStateChanged?.Invoke(_itemL, Arm.None);

        _armL.Visible = false;
        _armR.Visible = false;
        _itemL = null;
        _itemR = null;
    }

    private void AssignNewBoxShapeDeferredLeft() { CallDeferred(MethodName.AssignNewBoxShape, _springL, _armL, new Godot.Collections.Array<Aabb> { _armMeshInstL.GetAabb(), _itemL.GetAabb() }); }
    private void AssignNewBoxShapeDeferredRight() { CallDeferred(MethodName.AssignNewBoxShape, _springR, _armR, new Godot.Collections.Array<Aabb> { _armMeshInstR.GetAabb(), _itemR.GetAabb() }); }

    // Call deferred to allow AABB update
    private void AssignNewBoxShape(SpringArm3D spring, Node3D arm, Godot.Collections.Array<Aabb> aabbs)
    {
        BoxShape3D box = GenerateBoxShape(aabbs);
        
        spring.Shape = box;
        arm.Position = new(arm.Position.X, arm.Position.Y, -box.Size.Z * 0.5f);
    }

    private BoxShape3D GenerateBoxShape(Godot.Collections.Array<Aabb> aabbs)
    {
        Aabb mergedAabb = aabbs[0];
        for (int i = 1; i < aabbs.Count; i++)
        {
            mergedAabb = mergedAabb.Merge(aabbs[i]);
        }
        return new BoxShape3D() { Size = mergedAabb.Size };
    }
}