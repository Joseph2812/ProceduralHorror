using Godot;
using System;

namespace Scripts.Items;

public partial class Phone : Item
{
    private static readonly StringName _equipName = "Equip_Phone", _unequipName = "Unequip_Phone";
    private static readonly StringName _idleName = "Idle_Phone";

    protected override StringName FullEquipName => _equipName;
    protected override StringName FullUnequipName => _unequipName;
    protected override StringName FullIdleName => _idleName;
}
