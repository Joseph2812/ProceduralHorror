using Godot;
using System;
using Scripts.Extensions;
using Scripts.Generation.Interior;

namespace Scripts.Generation;

public class RoomManager
{
    private const string RoomsDirectory = "res://Generation/Rooms/";

    public static event Action RoomsLoaded;

    // TODO: Change to use configurations of room types
    public int MaximumRoomCount { get; private set; } = 30; // Max where generation will stop

    public Room SelectedRoom { get; private set; }

    private Room[] _rooms;

    public RoomManager()
    {
        string[] filenames = DirAccess.GetFilesAt(RoomsDirectory);

        _rooms = CommonMethods.LoadPaths<Room>(filenames, RoomsDirectory);
        SelectedRoom = _rooms[0];

        RoomsLoaded?.Invoke();
        RoomsLoaded = null;
    }

    public void SelectRandomRoom() { SelectedRoom = _rooms[MapGenerator.Inst.Rng.RandiRange(0, _rooms.Length - 1)]; }

    public InteriorObject GetRandomInteriorObject() => SelectedRoom.InteriorObjectsWithWeights.GetRandomElementByWeight(x => x.WeightOfPlacement).InteriorObject;
}
