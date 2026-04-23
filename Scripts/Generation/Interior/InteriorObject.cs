using Godot;
using Scripts.Extensions;
using System;
using System.Collections.Generic;

namespace Scripts.Generation.Interior;

// Must be a tool to be loaded in the editor by InteriorObjectCreator (applies to all custom resources used by it)
[GlobalClass, Tool]
public partial class InteriorObject : Resource
{
    public enum Relative
    {
        Floor,
        Middle,
        Ceiling
    }

    [Export]
    public PackedScene Scene { get; set; }

    [ExportGroup("Probability")]

    /// <summary>
    /// Likelihood of <see cref="Scene"/> appearing in a cell depending on its proxmity to the middle, from 0 (edge) to 1 (middle).
    /// </summary>
    [Export(PropertyHint.Range, "0,1")]
    public float WeightToMiddle { get; set; }

    /// <summary>
    /// If true, <see cref="Scene"/> will have a chance to appear in a cell only if <see cref="WeightToMiddle"/> is equal to normalised distance.<para/>
    /// Mainly only useful for <see cref="WeightToMiddle"/> = 0 or 1, as inbetween values aren't guaranteed to appear among the cells.
    /// </summary>
    [Export]
    public bool Exact { get; set; }

    [ExportGroup("Constraints")]

    /// <summary>
    /// Minimum height <see cref="Scene"/> needs to be at/above for it to be placed.
    /// </summary>
    [Export(PropertyHint.Range, "-2147483646,2147483646")]
    public int MinimumHeight { get; set; } = 1;

    /// <summary>
    /// Maximum height <see cref="Scene"/> needs to be at/below for it to be placed.
    /// </summary>
    [Export(PropertyHint.Range, "-2147483646,2147483646")]
    public int MaximumHeight { get; set; } = int.MaxValue - 1;

    /// <summary>
    /// Sets what <see cref="MinimumHeight"/> and <see cref="MaximumHeight"/> will be relative to with their values.<para/>
    /// 
    /// 1 <![CDATA[->]]> (<see cref="int.MaxValue"/> - 1): <see cref="Relative.Floor"/> and <see cref="Relative.Ceiling"/><br/>
    /// -(<see cref="int.MaxValue"/> - 1) <![CDATA[<- 0 ->]]> (<see cref="int.MaxValue"/> - 1): <see cref="Relative.Middle"/>
    /// </summary>
    [Export]
    public Relative RelativeTo { get; set; }

    /// <summary>
    /// Maximum times of <see cref="Scene"/> that can be placed from this <see cref="InteriorObject"/> instance.<br/>
    /// Use to set limits across all the rooms.<br/>
    /// 0 = There is no maximum count restriction.
    /// </summary>
    [Export(PropertyHint.Range, "0,2147483647")]
    public int MaximumCountBtwRooms { get; set; }

    /// <summary>
    /// String that represents a boolean expression. Should only be passed to <see cref="_neighbourConditions"/> to parse.
    /// </summary>
    [Export(PropertyHint.MultilineText)]
    public string NeighbourConditionsText { get; set; } = string.Empty;

    /// <summary>
    /// Used to mark what relative positions it will take up when placed. (0, 0, 0) will already be added for any <see cref="Scene"/>.<para/>
    /// </summary>
    [Export]
    public Godot.Collections.Array<Vector3I> ClearancePositions { get; set; } = [];

    /// <summary>
    /// Used to mark what relative positions it would want clear, but doesn't take up that space itself.
    /// </summary>
    [Export]
    public Godot.Collections.Array<Vector3I> SemiClearancePositions { get; set; } = [];

    [ExportGroup("Rotation")]

    // Random offset that should be added to the proximity-based rotation. (Difference of 360 makes it completely random)
    [Export(PropertyHint.Range, "-360,360,radians")]
    public float MinimumRotationalYOffset { get; set; }
    
    [Export(PropertyHint.Range, "-360,360,radians")]
    public float MaximumRotationalYOffset { get; set; }

    private int _currentCountBtwRooms;
    private readonly NeighbourConditions _neighbourConditions = new();

    /// <summary>
    /// Use to load any dependencies after this object has been loaded from file.<para/>
    /// If using <see cref="ResourceLoader.CacheMode.Reuse"/>, then call <see cref="IsDependenciesLoaded(InteriorObject)"/> to first check if it hasn't already been loaded before using this.
    /// </summary>
    protected virtual void LoadDependencies() { _neighbourConditions.ParseIntoTree(NeighbourConditionsText); }

    public override void _Notification(int what)
    {
        base._Notification(what);

        if (what != NotificationPredelete) { return; }
        RoomManager.LoadedIObjDependencies.Remove(this);
    }

    public void CheckDependencies()
    {
        if (RoomManager.LoadedIObjDependencies.Contains(this)) { return; }
        RoomManager.LoadedIObjDependencies.Add(this);

        LoadDependencies();
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
            IsClearanceFullyContainedInEmptyPosS(emptyPosS, clearancePosS)                                                         &&
            (semiClearancePosS.Count == 0 || semiClearancePosS.IsSubsetOf(emptyPosS.Keys))                                         &&
            _neighbourConditions.IsSatisfied(MapGenerator.Inst.GetNeighbours(position, MapGenerator.Inst.All3x3x3Dirs), rotationY) &&
            IsNotMaxCountThenIncrement(),

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
        HashSet<Vector3I> clearancePosS = [originPos];
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
        HashSet<Vector3I> semiClearancePosS = [];
        foreach (Vector3I relativePos in SemiClearancePositions)
        {
            semiClearancePosS.Add(originPos + relativePos.RotatedY(rotationY));
        }
        return semiClearancePosS;
    }

    /// <summary>
    /// Check whether the max count of this instance has been reached, if not increment by 1.
    /// </summary>
    private bool IsNotMaxCountThenIncrement()
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
