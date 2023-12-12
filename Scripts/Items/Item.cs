using Godot;
using System;

namespace Scripts.Items;

public abstract partial class Item : RigidBody3D
{
    private static readonly Vector2I[] _defaultClearancePositions = new Vector2I[] { Vector2I.Zero };

    private static readonly StringName _equipName = "Equip", _unequipName = "Unequip";
    private static readonly StringName _idleName = "Idle";

    public abstract bool TwoHanded { get; }
    protected abstract StringName FullEquipName { get; }
    protected abstract StringName FullUnequipName { get; }
    protected abstract StringName FullIdleName { get; }

    /// <summary>
    /// Local grid coordinates used by <see cref="Player.Inventory"/> to indicate the positions it takes up.
    /// </summary>
    public virtual Vector2I[] ClearancePositions => _defaultClearancePositions;

    public CollisionShape3D CollisionShape { get; private set; }

    protected bool Equipped { get; private set; }

    private AnimationPlayer _itemAnim;
    private AnimationPlayer[] _otherAnims;

    public virtual void Equip(AnimationPlayer[] otherAnims)
    {
        Equipped = true;
        _otherAnims = otherAnims;

        Visible = true;
        //PlayAnimation(_equipName, FullEquipName);
        PlayAnimation(_idleName, FullIdleName);

    }
    public virtual void Unequip()
    {
        Equipped = false;

        // Would need to wait for this to finish, so a different equip animation could play right after when pressing hotkey
        //PlayAnimation(_unequipName, FullUnequipName);

        Visible = false;
    }

    public override void _Ready()
    {
        base._Ready();

        CollisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
        _itemAnim = GetNode<AnimationPlayer>("AnimationPlayer");
    }

    protected void PlayAnimation(StringName name, StringName fullName)
    {
        _itemAnim.Play(name);
        foreach (AnimationPlayer anim in _otherAnims) { anim.Play(fullName); }
    }
}
