using Godot;
using System;

namespace Scripts.Pickups;

/// <summary>
/// Base class that contains the required fields to be added to the inventory.
/// </summary>
public abstract partial class Pickup : RigidBody3D
{
    /// <summary>
    /// Visual rotation applied to the <see cref="MeshInstance3D"/> in <see cref="Inventory"/> when placing it on the grid.
    /// </summary>
    [Export(PropertyHint.Range, "-360,360,radians")]
    public Vector3 InventoryRotation { get; private set; }

    /// <summary>
    /// Local grid coordinates used by <see cref="Inventory"/> to indicate the positions it takes up, starting from the bottom left.<para/>
    /// NOTE: Make sure this matches up with the visual rotation applied by <see cref="InventoryRotation"/>.
    /// </summary>
    [Export] public Godot.Collections.Array<Vector2I> ClearancePositions { get; private set; } = [Vector2I.Zero];

    public MeshInstance3D MeshInstance { get; private set; }
    public BaseMaterial3D Material { get; private set; }
    public CollisionShape3D CollisionShape { get; private set; }

    public override void _Ready()
    {
        base._Ready();

        int count = GetChildCount();
        for (int i = 0; i < count; i++)
        {
            if (GetChild(i) is MeshInstance3D mesh)
            {
                MeshInstance = mesh;
                break;
            }
        }

        Material = (BaseMaterial3D)MeshInstance.GetActiveMaterial(0);
        Material.NextPass = GD.Load<Material>("Materials/Outline.tres");
        CollisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
    }

    /// <summary>
    /// Gets a box that encloses the <see cref="ClearancePositions"/> in terms of inventory slots.
    /// </summary>
    public Vector2I GetInventorySize()
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = 0, maxY = 0;

        foreach (Vector2I pos in ClearancePositions)
        {
            if (pos.X < minX) { minX = pos.X; }
            if (pos.Y < minY) { minY = pos.Y; }
            
            if (pos.X > maxX) { maxX = pos.X; }
            if (pos.Y > maxY) { maxY = pos.Y; }
        }
        return new(maxX - minX + 1, maxY - minY + 1);
    }
}
