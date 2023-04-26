using Godot;
using System;

namespace Scripts.Generation;

public class RoomManager
{
    private const string RoomsDirectory = "res://Rooms";

    public ItemManager.ItemId FloorId => _selectedRoom.FloorId;
    public ItemManager.ItemId WallId => _selectedRoom.WallId;
    public ItemManager.ItemId CeilingId => _selectedRoom.CeilingId;

    private RandomNumberGenerator _random;
    private Room[] _rooms;
    private Room _selectedRoom;

    public RoomManager(RandomNumberGenerator random)
    {
        _random = random;

        string[] filenames = DirAccess.GetFilesAt(RoomsDirectory);

        _rooms = new Room[filenames.Length];
        for (int i = 0; i < filenames.Length; i++)
        {
            _rooms[i] = GD.Load<Room>(RoomsDirectory + "/" + filenames[i]);
        }
        _selectedRoom = _rooms[0];
    }

    public void SelectRandomRoom() { _selectedRoom = _rooms[_random.RandiRange(0, _rooms.Length - 1)]; }

    public InteriorObject GetRandomInteriorObject() => _selectedRoom.ObjectPool[_random.RandiRange(0, _selectedRoom.ObjectPool.Length - 1)];
}
