using Godot;
using System;
using Scripts.Items;

namespace Scripts.Player;

public partial class ArmsManager : Node3D
{
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

    // These public methods should ONLY be called by Inventory //
    public void EquipLeft(Item item)
    {
        if (_itemL == item) { return; }
        if (_itemL != null) { Unequip(_itemL); }

        _itemL = item;

        item.Position = _armL.Position;
        item.Equip(_animL);
    }
    public void EquipRight(Item item)
    {
        if (_itemR == item) { return; }
        if (_itemR != null) { Unequip(_itemR); }

        _itemR = item;

        item.Position = _armR.Position;
        item.Equip(_animR);
    }
    public void EquipBoth(Item item)
    {
        if (_itemL == item) { return; } // Only one needs to be checked if it's two-handed
        if (_itemL != null) { Unequip(_itemL); }
        if (_itemR != null) { Unequip(_itemR); }

        _itemL = item;
        _itemR = item;

        item.Position = Vector3.Zero;
        item.Equip(_animBoth);
    }

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

        if (unequipped) { item.Unequip(); }
    }
}
