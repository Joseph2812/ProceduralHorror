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
    private AnimationPlayer[] _animL, _animR, _animBoth;
    private Item _itemL, _itemR;

    public override void _Ready()
    {
        base._Ready();

        _armL = GetNode<Node3D>("Arm_L");
        _armR = GetNode<Node3D>("Arm_R");

        AnimationPlayer animL = _armL.GetNode<AnimationPlayer>("AnimationPlayer");
        AnimationPlayer animR = _armR.GetNode<AnimationPlayer>("AnimationPlayer");

        _animL = new AnimationPlayer[] { animL };
        _animR = new AnimationPlayer[] { animR };
        _animBoth = new AnimationPlayer[] { animL, animR };

        GetParent().GetNode<Inventory>("Inventory").ItemRemoved += (item) => Unequip(item);
    }

    // These Public Methods Should ONLY Be Called By Inventory //
    public void EquipLeft(Item item)
    {
        if (_itemL == item)
        {
            UnequipLeftOnly();
            return;
        }
        if (_itemR == item) { UnequipRightOnly(); }
        if (_itemL != null) { Unequip(_itemL); }

        _itemL = item;

        item.Position = _armL.Position;

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
        if (_itemL == item) { UnequipLeftOnly(); }
        if (_itemR != null) { Unequip(_itemR); }

        _itemR = item;

        item.Position = _armR.Position;

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

        item.Position = Vector3.Zero;

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
            unequipped = true;
        }
        if (_itemR == item)
        {
            _itemR = null;
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

        _itemL = null;       
    }
    private void UnequipRightOnly()
    {
        _itemR.Unequip();
        EquippedStateChanged?.Invoke(_itemR, Arm.None);

        _itemR = null;
    }
    private void UnequipBothOnly()
    {
        _itemL.Unequip();
        EquippedStateChanged?.Invoke(_itemL, Arm.None);

        _itemL = null;
        _itemR = null;
    }
}