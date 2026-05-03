using Godot;
using System;

namespace Scripts.Pickups;

public partial class Junk : Pickup
{
    [Export] public int MetalValue { get; private set; }
    [Export] public int CeramicValue { get; private set; }
    [Export] public int PolymerValue { get; private set; }
    [Export] public int FuelValue { get; private set; }
}
