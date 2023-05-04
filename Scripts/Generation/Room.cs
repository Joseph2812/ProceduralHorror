using Godot;
using System;

namespace Scripts.Generation;

public partial class Room : Resource
{
    [Export] public ItemManager.Id FloorId   { get; private set; }
    [Export] public ItemManager.Id WallId    { get; private set; }
    [Export] public ItemManager.Id CeilingId { get; private set; }

    [Export] public InteriorObject[] ObjectPool { get; private set; }
}
