using Godot;
using System;

namespace Scripts.Items;

public partial class Phone : Item
{
    private static readonly StringName _equipNameL = "Equip_Phone_L", _equipNameR = "Equip_Phone_R";
    private static readonly StringName _idleNameL = "Idle_Phone_L", _idleNameR = "Idle_Phone_R";
    private static readonly StringName _unequipNameL = "Unequip_Phone_L", _unequipNameR = "Unequip_Phone_R";

    public override bool TwoHanded => false;

    public override StringName EquipNameL => _equipNameL;
    public override StringName EquipNameR => _equipNameR;
    public override StringName IdleNameL => _idleNameL;
    public override StringName IdleNameR => _idleNameR;
    public override StringName UnequipNameL => _unequipNameL;
    public override StringName UnequipNameR => _unequipNameR;

    protected override string MeshInstPath => "Phone_Obj";
}
