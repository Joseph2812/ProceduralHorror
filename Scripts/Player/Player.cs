using Godot;
using System;

namespace Scripts.Player;

public partial class Player : CharacterBody3D
{
    public static Player Inst { get; private set; }

    public Player() { Inst = this; }

    public override void _Ready()
    {
        base._Ready();

        Rid rid = GetRid();
        GetNode<ArmsManager>("Camera3D/ArmsManager").AddCollisionExclusion(rid);
        GetNode<InteractionController>("Camera3D/InteractionController").AddRayExclusion(rid);
    }
}
