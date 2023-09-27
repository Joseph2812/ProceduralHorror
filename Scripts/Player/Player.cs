using Godot;
using System;

namespace Scripts.Player;

public partial class Player : MovementController
{
    public static Player Inst { get; private set; }

    public Rid Rid { get; private set; }

    public Player() { Inst = this; }

    public override void _Ready()
    {
        base._Ready();

        Rid = GetRid();
    }
}
