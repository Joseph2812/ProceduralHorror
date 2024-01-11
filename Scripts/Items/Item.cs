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
    protected abstract string MeshInstPath { get; }

    /// <summary>
    /// Offset applied to the <see cref="MeshInstance3D"/> in <see cref="Player.Inventory"/> when placing it on the grid.
    /// </summary>
    public virtual Vector3 InventoryOffset => Vector3.Zero;

    /// <summary>
    /// Rotation applied to the <see cref="MeshInstance3D"/> in <see cref="Player.Inventory"/> when placing it on the grid.
    /// </summary>
    public virtual Vector3 InventoryRotation => Vector3.Zero;

    /// <summary>
    /// Local grid coordinates used by <see cref="Player.Inventory"/> to indicate the positions it takes up.
    /// </summary>
    public virtual Vector2I[] ClearancePositions => _defaultClearancePositions;

    /// <summary>
    /// Signals when idle animation has started, which should be a 1 frame pose (used to generate a new shape for <see cref="SpringArm3D"/>s).<para/>
    /// NOTE: Cleared on Unequip.
    /// </summary>
    public event Action IdleStarted;

    public Mesh InventoryMesh => _meshInst.Mesh;
    public BaseMaterial3D InventoryMaterial { get; private set; }
    public CollisionShape3D CollisionShape { get; private set; }

    protected bool Equipped { get; private set; }

    private MeshInstance3D _meshInst;
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
        IdleStarted = null;

        // Would need to wait for this to finish, so a different equip animation could play right after when pressing hotkey
        //PlayAnimation(_unequipName, FullUnequipName);

        Visible = false;
    }

    public override void _Ready()
    {
        base._Ready();

        _meshInst = GetNode<MeshInstance3D>(MeshInstPath);
        InventoryMaterial = (BaseMaterial3D)_meshInst.GetActiveMaterial(0);

        CollisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
        _itemAnim = GetNode<AnimationPlayer>("AnimationPlayer");

        _itemAnim.AnimationStarted += OnItemAnim_AnimationStarted;
    }

    public Aabb GetAabb() => _meshInst.GetAabb();

    protected void PlayAnimation(StringName name, StringName fullName)
    {
        _itemAnim.Play(name);
        foreach (AnimationPlayer anim in _otherAnims) { anim.Play(fullName); }
    }

    private void OnItemAnim_AnimationStarted(StringName name)
    {
        if (name == _idleName) { IdleStarted?.Invoke(); }
    }
}
