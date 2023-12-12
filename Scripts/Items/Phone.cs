using Godot;
using System;

namespace Scripts.Items;

public partial class Phone : Item
{
    private static readonly StringName _fullEquipName = "Equip_Phone", _fullUnequipName = "Unequip_Phone";
    private static readonly StringName _fullIdleName = "Idle_Phone";

    public override bool TwoHanded => false;
    protected override StringName FullEquipName => _fullEquipName;
    protected override StringName FullUnequipName => _fullUnequipName;
    protected override StringName FullIdleName => _fullIdleName;
}
