using Godot;
using System;
using Scripts.Extensions;
using Scripts.Generation.Interior;
using System.Text;
using System.Collections.Generic;

namespace Scripts.Generation;

public class RoomManager
{
    private const string RoomsDirectory = "res://Generation/Rooms/";

    // Tracks what dependencies are loaded once, to stop cyclic looping (must be outside [Tool], so it resets at every run when using the editor to run it)
    public static readonly HashSet<InteriorObject> LoadedIObjDependencies = new();

    // TODO: Change to use configurations of room types
    public int MaximumRoomCount { get; private set; } = 30; // Max where generation will stop
    public Room SelectedRoom { get; private set; }

    private Room[] _rooms;

    public RoomManager()
    {
        string[] directories = DirAccess.GetDirectoriesAt(RoomsDirectory);

        _rooms = new Room[directories.Length];
        for (int i = 0; i < directories.Length; i++)
        {
            StringBuilder strBuilder = new(RoomsDirectory);
            strBuilder.Append(directories[i]);
            strBuilder.Append('/');
            strBuilder.Append(DirAccess.GetFilesAt(strBuilder.ToString())[0]);

            Room room = GD.Load<Room>(strBuilder.ToString());
            room.LoadIObjWithWeightS();

            _rooms[i] = room;
        }
        SelectedRoom = _rooms[0];
    }

    public void SelectRandomRoom() { SelectedRoom = _rooms[MapGenerator.Inst.Rng.RandiRange(0, _rooms.Length - 1)]; }

    public InteriorObject GetRandomInteriorObject() => SelectedRoom.InteriorObjectWithWeightS.GetRandomElementByWeight(x => x.WeightOfPlacement).InteriorObject;
}
