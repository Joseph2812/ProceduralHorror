using Godot;
using System;

namespace Scripts.Generation;

public partial class Room : Resource
{
    [Export] public ItemManager.ItemId FloorId   { get; private set; }
    [Export] public ItemManager.ItemId WallId    { get; private set; }
    [Export] public ItemManager.ItemId CeilingId { get; private set; }

    [Export] public InteriorObject[] ObjectPool { get; private set; }
}
