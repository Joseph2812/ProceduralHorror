using Godot;
using Scripts.Player;
using System;

namespace Scripts.Items;

public abstract partial class Item : RigidBody3D
{
    private static readonly Vector2I[] s_defaultClearancePositions = new Vector2I[] { Vector2I.Zero };

    public abstract bool TwoHanded { get; }

    protected abstract StringName EquipNameL { get; }
    protected abstract StringName EquipNameR { get; }
    protected abstract StringName IdleNameL { get; }
    protected abstract StringName IdleNameR { get; }
    protected abstract StringName UnequipNameL { get; }
    protected abstract StringName UnequipNameR { get; }

    protected abstract string MeshInstPath { get; }

    /// <summary>
    /// Visual offset applied to the <see cref="MeshInstance3D"/> in <see cref="Inventory"/> when placing it on the grid.
    /// </summary>
    public virtual Vector3 InventoryOffset => Vector3.Zero;

    /// <summary>
    /// Visual rotation applied to the <see cref="MeshInstance3D"/> in <see cref="Inventory"/> when placing it on the grid.
    /// </summary>
    public virtual Vector3 InventoryRotation => Vector3.Zero;

    /// <summary>
    /// Local grid coordinates used by <see cref="Inventory"/> to indicate the positions it takes up.
    /// </summary>
    public virtual Vector2I[] ClearancePositions => s_defaultClearancePositions;

    public MeshInstance3D MeshInstance { get; set; }
    public BaseMaterial3D Material { get; private set; }
    public CollisionShape3D CollisionShape { get; private set; }

    protected bool Equipped { get; private set; }
    protected AnimationPlayer OtherAnim { get; set; }

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

        MeshInstance = GetNode<MeshInstance3D>(MeshInstPath);
        Material = (BaseMaterial3D)MeshInstance.GetActiveMaterial(0);
        CollisionShape = GetNode<CollisionShape3D>("CollisionShape3D");

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
