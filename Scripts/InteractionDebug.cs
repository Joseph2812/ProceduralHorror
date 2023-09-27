using Godot;
using System;

namespace Scripts;

public partial class InteractionDebug : RigidBody3D, IInteractable
{
    public bool ExitRequired => true;

    public void Interact()
    {
        GD.Print("InteractRecieved");
    }

    public void Exit()
    {
        GD.Print("ExitRecieved");
    }
}
