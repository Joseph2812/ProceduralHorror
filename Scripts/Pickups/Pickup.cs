using Godot;
using System;

namespace Scripts.Pickups;

/// <summary>
/// Base class that contains the required fields to be added to the inventory.
/// </summary>
public abstract partial class Pickup : RigidBody3D
{
    private static readonly Vector2I[] s_defaultClearancePositions = [Vector2I.Zero];

    public MeshInstance3D MeshInstance { get; private set; }
    public BaseMaterial3D Material { get; private set; }
    public CollisionShape3D CollisionShape { get; private set; }

    /// <summary>
    /// Visual offset applied to the <see cref="MeshInstance3D"/> in <see cref="Inventory"/> when placing it on the grid.
    /// </summary>
    public Vector3 InventoryOffset { get; protected set; } = Vector3.Zero;

    /// <summary>
    /// Visual rotation applied to the <see cref="MeshInstance3D"/> in <see cref="Inventory"/> when placing it on the grid.
    /// </summary>
    public Vector3 InventoryRotation { get; protected set; } = Vector3.Zero;

    /// <summary>
    /// Local grid coordinates used by <see cref="Inventory"/> to indicate the positions it takes up.
    /// </summary>
    public Vector2I[] ClearancePositions { get; protected set; } = s_defaultClearancePositions;

    public override void _Ready()
    {
        base._Ready();

        Godot.Collections.Array<Node> children = GetChildren();
        foreach (Node n in children)
        {
            if (n is MeshInstance3D mesh)
            {
                MeshInstance = mesh;
                break;
            }
        }
        Material = (BaseMaterial3D)MeshInstance.GetActiveMaterial(0);
        CollisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
    }
}
