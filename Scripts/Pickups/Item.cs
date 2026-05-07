using Godot;
using Scripts.Player;
using System;

namespace Scripts.Pickups;

public abstract partial class Item : Pickup
{
    public abstract bool TwoHanded { get; }

    protected abstract StringName EquipNameL { get; }
    protected abstract StringName EquipNameR { get; }
    protected abstract StringName IdleNameL { get; }
    protected abstract StringName IdleNameR { get; }
    protected abstract StringName UnequipNameL { get; }
    protected abstract StringName UnequipNameR { get; }

    protected AnimationPlayer OtherAnim { get; set; }
    protected bool Equipped { get; private set; }

    private AnimationPlayer _itemAnim;
    private ArmsManager.Arm _currentArm;

    public virtual void Equip(ArmsManager.Arm arm)
    {
        Equipped = true;
        Visible = true;
        _currentArm = arm;

        PlayAnimation(EquipNameL, EquipNameR);
    }

    public virtual void Unequip()
    {
        Equipped = false;
        PlayAnimation(UnequipNameL, UnequipNameR);
    }

    public override void _Ready()
    {
        base._Ready();
        
        _itemAnim = GetNode<AnimationPlayer>("AnimationPlayer");
        _itemAnim.AnimationFinished += OnItemAnim_AnimationFinished;
    }

    /// <summary>
    /// To play animations at the same time (syncs: item, arm, and other)
    /// </summary>
    protected void PlayAnimation(StringName nameL, StringName nameR)
    {
        switch (_currentArm)
        {
            case ArmsManager.Arm.Left:
                _itemAnim.Play(nameL);
                OtherAnim?.Play(nameL);
                ArmsManager.ArmAnimL.Play(nameL);          

                ArmsManager.ArmAnimL.Advance(0d); // Advance(0): To make sure AnimationFinished events always fire, otherwise misses at the end of a new animation, when a new animation is played right after the previous is finished
                break;

            case ArmsManager.Arm.Right:
                _itemAnim.Play(nameR);
                OtherAnim?.Play(nameR);
                ArmsManager.ArmAnimR.Play(nameR);

                ArmsManager.ArmAnimR.Advance(0d);
                break;

            case ArmsManager.Arm.Both:
                _itemAnim.Play(nameL);
                OtherAnim?.Play(nameL);
                ArmsManager.ArmAnimL.Play(nameL);
                ArmsManager.ArmAnimR.Play(nameR);

                ArmsManager.ArmAnimL.Advance(0d);
                ArmsManager.ArmAnimR.Advance(0d);
                break;

            default:
                throw new NotImplementedException("Invalid arm value.");
        }
    }

    private void OnItemAnim_AnimationFinished(StringName animName)
    {
        if      (animName == EquipNameL   || animName == EquipNameR)   { PlayAnimation(IdleNameL, IdleNameR); }
        else if (animName == UnequipNameL || animName == UnequipNameR) { Visible = false; }
    }
}
