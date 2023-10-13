using Godot;
using System;

namespace Scripts.Items;

public partial class Item : RigidBody3D
{
    /// <summary>
    /// Local grid coordinates used by <see cref="Player.Inventory"/> to indicate the positions it takes up.
    /// </summary>
    public Vector2I[] ClearancePositions { get; } = new Vector2I[] { Vector2I.Zero, Vector2I.Down };

    private bool _equipped;

    public virtual void Equip()
    {
        _equipped = true;
    }
    public virtual void Unequip()
    {
        _equipped = false;
    }
}
