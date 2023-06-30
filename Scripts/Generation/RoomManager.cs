using Godot;
using System;
using Scripts.Extensions;
using Scripts.Generation.Interior;

namespace Scripts.Generation;

public class RoomManager
{
    private const string RoomsDirectory = "res://Generation/Rooms/";

    public static event Action RoomsLoaded;

    public ItemManager.Id FloorId => _selectedRoom.FloorId;
    public ItemManager.Id WallId => _selectedRoom.WallId;
    public ItemManager.Id CeilingId => _selectedRoom.CeilingId;
    public float ChanceOfEmptyCell => _selectedRoom.ChanceOfEmptyCell;

    private Room[] _rooms;
    private Room _selectedRoom;

    public RoomManager()
    {
        string[] filenames = DirAccess.GetFilesAt(RoomsDirectory);

        _rooms = CommonMethods.LoadPaths<Room>(filenames, RoomsDirectory);
        _selectedRoom = _rooms[0];

        RoomsLoaded?.Invoke();
        RoomsLoaded = null;
    }

    public void SelectRandomRoom() { _selectedRoom = _rooms[MapGenerator.Inst.Rng.RandiRange(0, _rooms.Length - 1)]; }

    public InteriorObject GetRandomInteriorObject() => _selectedRoom.InteriorObjects.GetRandomElementByWeight(x => x.WeightOfPlacement);
}
