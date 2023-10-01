using Godot;
using Scripts.Extensions;
using System;
using System.Collections.Generic;

namespace Scripts.Generation.Interior;

[GlobalClass]
[Tool]
public partial class InteriorObject : Resource
{
    public enum Relative
    {
        Floor,
        Middle,
        Ceiling
    }

    public PackedScene Scene { get; set; }

    /// <summary>
    /// Likelihood of <see cref="Scene"/> appearing in a cell depending on its proxmity to the middle, from 0 (edge) to 1 (middle).
    /// </summary>
    public float WeightToMiddle { get; set; }

    /// <summary>
    /// If true, <see cref="Scene"/> will have a chance to appear in a cell only if <see cref="WeightToMiddle"/> is equal to normalised distance.<para/>
    /// Mainly only useful for <see cref="WeightToMiddle"/> = 0 or 1, as inbetween values aren't guaranteed to appear among the cells.
    /// </summary>
    public bool Exact { get; set; }

    /// <summary>
    /// Minimum height <see cref="Scene"/> needs to be at/above for it to be placed.
    /// </summary>
    public int MinimumHeight { get; set; } = 1;

    /// <summary>
    /// Maximum height <see cref="Scene"/> needs to be at/below for it to be placed.
    /// </summary>
    public int MaximumHeight { get; set; } = int.MaxValue - 1;

    /// <summary>
    /// Sets what <see cref="MinimumHeight"/> and <see cref="MaximumHeight"/> will be relative to with their values.<para/>
    /// 
    /// 1 <![CDATA[->]]> (<see cref="int.MaxValue"/> - 1): <see cref="Relative.Floor"/> and <see cref="Relative.Ceiling"/><br/>
    /// -(<see cref="int.MaxValue"/> - 1) <![CDATA[<- 0 ->]]> (<see cref="int.MaxValue"/> - 1): <see cref="Relative.Middle"/>
    /// </summary>
    public Relative RelativeTo { get; set; }

    /// <summary>
    /// Maximum times of <see cref="Scene"/> that can be placed from this <see cref="InteriorObject"/> instance.<br/>
    /// Use to set limits across all the rooms.<br/>
    /// 0 = There is no maximum count restriction.
    /// </summary>
    public int MaximumCountBtwRooms { get; set; }

    /// <summary>
    /// String that represents a boolean expression. Should only be passed to <see cref="_neighbourConditions"/> to parse.
    /// </summary>
    public string NeighbourConditionsText { get; set; } = string.Empty;

    /// <summary>
    /// Used to mark what relative positions it will take up when placed. (0, 0, 0) will already be added for any <see cref="Scene"/>.<para/>
    /// Godot doesn't support exporting Vector3I[] :(
    /// </summary>
    public Vector3[] ClearancePositions { get; set; } = Array.Empty<Vector3>();

    /// <summary>
    /// Used to mark what relative positions it would want clear, but doesn't take up that space itself.
    /// </summary>
    public Vector3[] SemiClearancePositions { get; set; } = Array.Empty<Vector3>();

    // Random offset that should be added to the proximity-based rotation. (Difference of 360 makes it completely random)
    public float MinimumRotationalYOffset { get; set; }
    public float MaximumRotationalYOffset { get; set; }

    private int _currentCountBtwRooms;
    private readonly NeighbourConditions _neighbourConditions = new();

    public static bool IsDependenciesLoaded(InteriorObject iObj) => RoomManager.LoadedInteriorObjects.Contains(iObj);

    ~InteriorObject() { RoomManager.LoadedInteriorObjects.Remove(this); }

    public override bool _PropertyCanRevert(StringName property) => true;
    public override Variant _PropertyGetRevert(StringName property)
    {
        switch (property)
        {
            case nameof(Scene)                   : return default;
            case nameof(WeightToMiddle)          : return 0f;
            case nameof(Exact)                   : return false;
            case nameof(MinimumHeight)           : return 1;
            case nameof(MaximumHeight)           : return int.MaxValue - 1;
            case nameof(RelativeTo)              : return 0;
            case nameof(MaximumCountBtwRooms)    : return 0;
            case nameof(NeighbourConditionsText) : return string.Empty;
            case nameof(ClearancePositions)      : return Array.Empty<Vector3>();
            case nameof(SemiClearancePositions)  : return Array.Empty<Vector3>();
            case nameof(MinimumRotationalYOffset): return 0f;
            case nameof(MaximumRotationalYOffset): return 0f;
            default                              : return base._PropertyGetRevert(property);
        }
    }

    public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetPropertyList()
    {
        return new
        (
            new Godot.Collections.Dictionary[]
            {
                CommonMethods.GetCategory(nameof(InteriorObject)),
                new Godot.Collections.Dictionary
                {
                    { "name"       , nameof(Scene) },
                    { "type"       , (int)Variant.Type.Object },
                    { "hint"       , (int)PropertyHint.TypeString },
                    { "hint_string", $"{(int)Variant.Type.Object}/{(int)PropertyHint.ResourceType}:{nameof(PackedScene)}" }
                },

                CommonMethods.GetGroup("Probability"),
                new Godot.Collections.Dictionary
                {
                    { "name"       , nameof(WeightToMiddle) },
                    { "type"       , (int)Variant.Type.Float },
                    { "hint"       , (int)PropertyHint.Range },
                    { "hint_string", "0,1" }
                },
                new Godot.Collections.Dictionary
                {
                    { "name", nameof(Exact) },
                    { "type", (int)Variant.Type.Bool }
                },

                CommonMethods.GetGroup("Constraints"),
                new Godot.Collections.Dictionary
                {
                    { "name"       , nameof(MinimumHeight) },
                    { "type"       , (int)Variant.Type.Int },
                    { "hint"       , (int)PropertyHint.Range },
                    { "hint_string", $"{-(int.MaxValue - 1)},{int.MaxValue - 1}"}
                },
                new Godot.Collections.Dictionary
                {
                    { "name"       , nameof(MaximumHeight) },
                    { "type"       , (int)Variant.Type.Int },
                    { "hint"       , (int)PropertyHint.Range },
                    { "hint_string", $"{-(int.MaxValue - 1)},{int.MaxValue - 1}"}
                },
                new Godot.Collections.Dictionary
                {
                    { "name"       , nameof(RelativeTo) },
                    { "type"       , (int)Variant.Type.Int },
                    { "hint"       , (int)PropertyHint.Enum },
                    { "hint_string", Enum.GetNames<Relative>().Join(",") }
                },

                new Godot.Collections.Dictionary
                {
                    { "name"       , nameof(MaximumCountBtwRooms) },
                    { "type"       , (int)Variant.Type.Int },
                    { "hint"       , (int)PropertyHint.Range },
                    { "hint_string", $"0,{int.MaxValue}" }
                },
                new Godot.Collections.Dictionary
                {
                    { "name"       , nameof(NeighbourConditionsText) },
                    { "type"       , (int)Variant.Type.String },
                    { "hint"       , (int)PropertyHint.MultilineText },
                },
                new Godot.Collections.Dictionary
                {
                    { "name", nameof(ClearancePositions) },
                    { "type", (int)Variant.Type.PackedVector3Array }
                },
                new Godot.Collections.Dictionary
                {
                    { "name", nameof(SemiClearancePositions) },
                    { "type", (int)Variant.Type.PackedVector3Array }
                },

                CommonMethods.GetGroup("Rotation"),
                new Godot.Collections.Dictionary
                {
                    { "name"       , nameof(MinimumRotationalYOffset) },
                    { "type"       , (int)Variant.Type.Float },
                    { "hint"       , (int)PropertyHint.Range },
                    { "hint_string", "-360,360,radians" }
                },
                new Godot.Collections.Dictionary
                {
                    { "name"       , nameof(MaximumRotationalYOffset) },
                    { "type"       , (int)Variant.Type.Float },
                    { "hint"       , (int)PropertyHint.Range },
                    { "hint_string", "-360,360,radians" }
                }
            }
        );
    }

    /// <summary>
    /// Use to load any dependencies after this object has been loaded from file.<para/>
    /// If using <see cref="ResourceLoader.CacheMode.Reuse"/>, then call <see cref="IsDependenciesLoaded(InteriorObject)"/> to first check if it hasn't already been loaded before using this.
    /// </summary>
    public virtual void LoadDependencies()
    {
        RoomManager.LoadedInteriorObjects.Add(this);
        _neighbourConditions.ParseIntoTree(NeighbourConditionsText);
    }

    /// <summary>
    /// Proximity determined rotationY with random offset. (Offset will not affect clearance positions, so make sure the object still fits in its assigned space)
    /// </summary>
    public float GetRotationWithOffset(float rotationY) => rotationY + MapGenerator.Inst.Rng.RandfRange(MinimumRotationalYOffset, MaximumRotationalYOffset);

    /// <returns>(Whether it can be placed, Clearance Positions, Semi-Clearance Positions)</returns>
    public (bool, HashSet<Vector3I>, HashSet<Vector3I>) CanBePlaced(Vector3I position, float rotationY, Dictionary<Vector3I, bool> emptyPosS)
    {
        HashSet<Vector3I> clearancePosS = GetClearancePositions(position, rotationY);
        HashSet<Vector3I> semiClearancePosS = GetSemiClearancePositions(position, rotationY);

        return
        (
            IsClearanceFullyContainedInEmptyPosS(emptyPosS, clearancePosS)                                                                       &&
            (semiClearancePosS.Count == 0 || semiClearancePosS.IsProperSubsetOf(emptyPosS.Keys) || semiClearancePosS.IsSubsetOf(emptyPosS.Keys)) &&
            _neighbourConditions.IsSatisfied(MapGenerator.Inst.GetNeighbours(position, MapGenerator.Inst.All3x3x3Dirs), rotationY)               &&
            IsNotMaxCountAndIncrement(),

            clearancePosS,
            semiClearancePosS
        );
    }

    private bool IsClearanceFullyContainedInEmptyPosS(Dictionary<Vector3I, bool> emptyPosS, HashSet<Vector3I> clearancePosS)
    {
        foreach (Vector3I pos in clearancePosS)
        {
            // Clearance is only valid in fully empty cells
            if (!emptyPosS.TryGetValue(pos, out bool isSemiEmpty) || isSemiEmpty)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Gets the positions that <see cref="Scene"/> would take up for being placed at <paramref name="originPos"/> with a <paramref name="rotationY"/> in radians.
    /// </summary>
    /// <param name="originPos">Position it will be placed at.</param>
    /// <param name="rotationY">Rotation applied around the global y-axis in radians.</param>
    /// <returns><see cref="HashSet{Vector3I}"/> of positions that mark its requirement for placement.</returns>
    private HashSet<Vector3I> GetClearancePositions(Vector3I originPos, float rotationY)
    {
        HashSet<Vector3I> clearancePosS = new() { originPos };
        foreach (Vector3I relativePos in ClearancePositions)
        {
            clearancePosS.Add(originPos + relativePos.RotatedY(rotationY));
        }
        return clearancePosS;
    }

    /// <summary>
    /// Gets the positions that <see cref="Scene"/> would want empty (but not take up itself) for being placed at <paramref name="originPos"/> with a <paramref name="rotationY"/> in radians.
    /// </summary>
    /// <param name="originPos">Position it will be placed at.</param>
    /// <param name="rotationY">Rotation applied around the global y-axis in radians.</param>
    /// <returns><see cref="HashSet{Vector3I}"/> of positions that mark its requirement for placement.</returns>
    private HashSet<Vector3I> GetSemiClearancePositions(Vector3I originPos, float rotationY)
    {
        HashSet<Vector3I> semiClearancePosS = new();
        foreach (Vector3I relativePos in SemiClearancePositions)
        {
            semiClearancePosS.Add(originPos + relativePos.RotatedY(rotationY));
        }
        return semiClearancePosS;
    }

    /// <summary>
    /// Check whether the max count of this instance has been reached, if not increment by 1.
    /// </summary>
    private bool IsNotMaxCountAndIncrement()
    {
        if (MaximumCountBtwRooms == 0) { return true; }

        if (_currentCountBtwRooms >= MaximumCountBtwRooms) { return false; }
        else
        {
            _currentCountBtwRooms++;
            return true;
        }
    }
}
