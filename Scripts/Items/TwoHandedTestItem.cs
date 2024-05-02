using Godot;
using System;

namespace Scripts.Items;

public partial class TwoHandedTestItem : Item
{
    private static readonly StringName s_equipNameL = "Equip_Phone_L", s_equipNameR = "Equip_Phone_R";
    private static readonly StringName s_idleNameL = "Idle_Phone_L", s_idleNameR = "Idle_Phone_R";
    private static readonly StringName s_unequipNameL = "Unequip_Phone_L", s_unequipNameR = "Unequip_Phone_R";

    public override StringName EquipNameL => s_equipNameL;
    public override StringName EquipNameR => s_equipNameR;
    public override StringName IdleNameL => s_idleNameL;
    public override StringName IdleNameR => s_idleNameR;
    public override StringName UnequipNameL => s_unequipNameL;
    public override StringName UnequipNameR => s_unequipNameR;

    public override bool TwoHanded => true;

    protected override string MeshInstPath => "Phone_Obj";
}
