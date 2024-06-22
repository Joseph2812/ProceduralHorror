using Godot;
using System;

namespace Scripts.Items;

public partial class Phone : Item
{
    private static readonly StringName s_equipNameL = "Equip_Phone_L", s_equipNameR = "Equip_Phone_R";
    private static readonly StringName s_idleNameL = "Idle_Phone_L", s_idleNameR = "Idle_Phone_R";
    private static readonly StringName s_unequipNameL = "Unequip_Phone_L", s_unequipNameR = "Unequip_Phone_R";

    public override bool TwoHanded => false;

    protected override StringName EquipNameL => s_equipNameL;
    protected override StringName EquipNameR => s_equipNameR;
    protected override StringName IdleNameL => s_idleNameL;
    protected override StringName IdleNameR => s_idleNameR;
    protected override StringName UnequipNameL => s_unequipNameL;
    protected override StringName UnequipNameR => s_unequipNameR;

    protected override string MeshInstPath => "Phone_Obj";

    public override void _Ready()
    {
        base._Ready();

        OtherAnim = GetNode<AnimationPlayer>("SpotlightAnimation");
    }
}
