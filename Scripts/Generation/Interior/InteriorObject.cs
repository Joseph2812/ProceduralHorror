using Godot;
using Scripts.Extensions;
using System;
using System.Collections.Generic;

namespace Scripts.Generation.Interior;

[Tool]
public partial class InteriorObject : Resource
{
    public PackedScene Scene { get; private set; }

    /// <summary>
    /// Weight of appearance compared to other <see cref="InteriorObject"/>s in the same room.
    /// Set to 0 to disable it from placement.
    /// </summary>
    public int WeightOfPlacement { get; private set; } = 1;

    /// <summary>
    /// Likelihood of <see cref="Scene"/> appearing in a cell depending on its proxmity to the centre, from 0 (edge) to 1 (centre).
    /// </summary>
    public float WeightToCentre { get; private set; }

    /// <summary>
    /// If true, <see cref="Scene"/>  will have a chance to appear in a cell only if <see cref="WeightToCentre"/> is equal to normalised distance.<para/>
    /// Mainly only useful for <see cref="WeightToCentre"/> = 0 or 1, as inbetween values aren't guaranteed to appear among the cells.
    /// </summary>
    public bool Exact { get; private set; }

    /// <summary>
    /// Only allows <see cref="Scene"/>  to be at maximum height of the room for it to be placed.
    /// </summary>
    public bool OnlyCeiling
    {
        get => _onlyCeiling;
        private set
        {
            _onlyCeiling = value;
            NotifyPropertyListChanged();
        }
    }

    /// <summary>
    /// Minimum height <see cref="Scene"/>  needs to be at/above for it to be placed.
    /// </summary>
    public int MinimumHeight { get; private set; } = 1;

    /// <summary>
    /// Maximum height <see cref="Scene"/> needs to be at/below for it to be placed.
    /// </summary>
    public int MaximumHeight { get; private set; } = int.MaxValue - 1;

    /// <summary>
    /// Maximum times of <see cref="Scene"/> can be placed from this <see cref="InteriorObject"/> instance.<br/>
    /// Use to set limits across multiple rooms when assigning to room types.<br/>
    /// 0 = There is no max count restriction.
    /// </summary>
    private int _maximumCountBtwRooms;
    private int _currentCountBtwRooms; 

    /// <summary>
    /// String that represents a boolean expression. Should only be passed to <see cref="_neighbourConditions"/> to parse.
    /// </summary>
    private string _neighbourConditionsText
    {
        get => p_neighbourConditionsText;
        set
        {
            p_neighbourConditionsText = value;
            if (Engine.IsEditorHint()) { return; }

            _neighbourConditions.ParseIntoTree(value);
        }
    }
    private string p_neighbourConditionsText;

    /// <summary>
    /// Used to mark what relative positions it will take up when placed. (0, 0, 0) will already be added for any <see cref="Scene"/>.<para/>
    /// Godot doesn't support exporting Vector3I[] :(
    /// </summary>
    private Vector3[] _clearancePositions = Array.Empty<Vector3>();

    /// <summary>
    /// Used to mark what relative positions it would want clear, but doesn't take up that space itself.
    /// </summary>
    private Vector3[] _semiClearancePositions = Array.Empty<Vector3>();

    private bool _onlyCeiling;
    private readonly NeighbourConditions _neighbourConditions = new();

    // Random offset that should be added to the proximity-based rotation. (Difference of 360 makes it completely random)
    private float _minimumRotationalYOffset;
    private float _maximumRotationalYOffset;

    public override bool _PropertyCanRevert(StringName property) => true;
    public override Variant _PropertyGetRevert(StringName property)
    {
        switch (property)
        {
            case nameof(Scene)                       : return default;
            case nameof(WeightOfPlacement)           : return 1;
            case nameof(WeightToCentre)              : return 0f;
            case nameof(Exact)                       : return false;
            case nameof(OnlyCeiling)                 : return false;
            case nameof(MinimumHeight)               : return 1;
            case nameof(MaximumHeight)               : return int.MaxValue - 1;
            case nameof(_maximumCountBtwRooms)       : return 0;
            case nameof(_neighbourConditionsText)    : return string.Empty;
            case nameof(_clearancePositions)         : return Array.Empty<Vector3>();
            case nameof(_semiClearancePositions)     : return Array.Empty<Vector3>();
            case nameof(_minimumRotationalYOffset)   : return 0f;
            case nameof(_maximumRotationalYOffset)   : return 0f;
            default                                  : return base._PropertyGetRevert(property);
        }
    }

    public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetPropertyList()
    {
        PropertyUsageFlags minMaxHeight_Usage = PropertyUsageFlags.NoEditor;
        if (!OnlyCeiling)
        {
            minMaxHeight_Usage = PropertyUsageFlags.Default;
        }

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
                    { "name"       , nameof(WeightOfPlacement) },
                    { "type"       , (int)Variant.Type.Int },
                    { "hint"       , (int)PropertyHint.Range },
                    { "hint_string", $"0,{int.MaxValue}" }
                },
                new Godot.Collections.Dictionary
                {
                    { "name"       , nameof(WeightToCentre) },
                    { "type"       , (int)Variant.Type.Float },
                    { "hint"       , (int)PropertyHint.Range },
                    { "hint_string", "0,1,0.01" }
                },
                new Godot.Collections.Dictionary
                {
                    { "name", nameof(Exact) },
                    { "type", (int)Variant.Type.Bool }
                },

                CommonMethods.GetGroup("Constraints"),
                new Godot.Collections.Dictionary
                {
                    { "name", nameof(OnlyCeiling) },
                    { "type", (int)Variant.Type.Bool }
                },
                new Godot.Collections.Dictionary
                {
                    { "name"       , nameof(MinimumHeight) },
                    { "type"       , (int)Variant.Type.Int },
                    { "usage"      , (int)minMaxHeight_Usage },
                    { "hint"       , (int)PropertyHint.Range },
                    { "hint_string", $"1,{int.MaxValue - 1}" }
                },
                new Godot.Collections.Dictionary
                {
                    { "name"       , nameof(MaximumHeight) },
                    { "type"       , (int)Variant.Type.Int },
                    { "usage"      , (int)minMaxHeight_Usage },
                    { "hint"       , (int)PropertyHint.Range },
                    { "hint_string", $"1,{int.MaxValue - 1}" }
                },
                new Godot.Collections.Dictionary
                {
                    { "name"       , nameof(_maximumCountBtwRooms) },
                    { "type"       , (int)Variant.Type.Int },
                    { "hint"       , (int)PropertyHint.Range },
                    { "hint_string", $"0,{int.MaxValue}" }
                },
                new Godot.Collections.Dictionary
                {
                    { "name"       , nameof(_neighbourConditionsText) },
                    { "type"       , (int)Variant.Type.String },
                    { "hint"       , (int)PropertyHint.MultilineText },
                },
                new Godot.Collections.Dictionary
                {
                    { "name", nameof(_clearancePositions) },
                    { "type", (int)Variant.Type.PackedVector3Array }
                },
                new Godot.Collections.Dictionary
                {
                    { "name", nameof(_semiClearancePositions) },
                    { "type", (int)Variant.Type.PackedVector3Array }
                },

                CommonMethods.GetGroup("Rotation"),
                new Godot.Collections.Dictionary
                {
                    { "name"       , nameof(_minimumRotationalYOffset) },
                    { "type"       , (int)Variant.Type.Float },
                    { "hint"       , (int)PropertyHint.Range },
                    { "hint_string", "-360,360,1,radians" }
                },
                new Godot.Collections.Dictionary
                {
                    { "name"       , nameof(_maximumRotationalYOffset) },
                    { "type"       , (int)Variant.Type.Float },
                    { "hint"       , (int)PropertyHint.Range },
                    { "hint_string", "-360,360,1,radians" }
                }
            }
        );
    }

    /// <summary>
    /// Proximity determined rotationY with random offset. (Offset will not effect clearance positions, so make sure the object still fits in its assigned space)
    /// </summary>
    public float GetRotationWithOffset(float rotationY) => rotationY + MapGenerator.Inst.Rng.RandfRange(_minimumRotationalYOffset, _maximumRotationalYOffset);

    /// <returns>(Whether it can be placed, Clearance Positions, Semi-Clearance Positions)</returns>
    public (bool, HashSet<Vector3I>, HashSet<Vector3I>) CanBePlaced(Vector3I position, float rotationY, Dictionary<Vector3I, bool> emptyPosS)
    {
        HashSet<Vector3I> clearancePosS = GetClearancePositions(position, rotationY);
        HashSet<Vector3I> semiClearancePosS = GetSemiClearancePositions(position, rotationY);

        return
        (
            IsClearanceFullyContainedInEmptyPosS(emptyPosS, clearancePosS)                                                                            &&
            (semiClearancePosS.Count == 0 || semiClearancePosS.IsProperSubsetOf(emptyPosS.Keys) || semiClearancePosS.IsSubsetOf(emptyPosS.Keys)) &&
            IsNeighboursValid(MapGenerator.Inst.GetNeighbours(position, MapGenerator.Inst.All3x3x3Dirs), rotationY)                              &&
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
        foreach (Vector3I relativePos in _clearancePositions)
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
        foreach (Vector3I relativePos in _semiClearancePositions)
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
        if (_maximumCountBtwRooms == 0) { return true; }

        if (_currentCountBtwRooms == _maximumCountBtwRooms) { return false; }
        else
        {
            _currentCountBtwRooms++;
            return true;
        }
    }

    /// <summary>
    /// Check whether the conditions defined in <see cref="NeighbourConditions"/> are met.
    /// </summary>
    /// <param name="all3x3x3Neighbours"></param>
    /// <param name="rotationY"></param>
    private bool IsNeighboursValid(NeighbourInfo[] all3x3x3Neighbours, float rotationY) => _neighbourConditions.IsSatisfied(all3x3x3Neighbours, rotationY);
}
