using Godot;
using System;

namespace Scripts;

public interface IInteractable
{
    bool ExitRequired { get; }

    void Interact();
    void Exit();
}
