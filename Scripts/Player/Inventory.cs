using Godot;
using System;
using System.Collections.Generic;
using Scripts.Items;
using Scripts.Extensions;

namespace Scripts.Player;

public partial class Inventory : Node3D
{
    private class GridData
    {
        public readonly Item Item;
        public readonly MeshInstance3D MeshInstance;
        public readonly ArrayMesh SelectionMesh;

        public Vector2I GridPosition { get; private set; }

        public static HashSet<Vector2I> GetOccupiedPositions(Vector2I[] clearancePosS, Vector2I gridPos, float rotationZ)
        {
            HashSet<Vector2I> positions = new(clearancePosS.Length);
            foreach (Vector2I pos in clearancePosS)
            {
                positions.Add(gridPos + pos.RotatedZ(rotationZ));
            }
            return positions;
        }

        public GridData(Item item, MeshInstance3D meshInstance, ArrayMesh selectionMesh)
        {
            Item = item;
            MeshInstance = meshInstance;
            SelectionMesh = selectionMesh;
        }

        public HashSet<Vector2I> GetOccupiedPositions() => GetOccupiedPositions(Item.ClearancePositions, GridPosition, MeshInstance.Rotation.Z);
        public HashSet<Vector2I> GetOccupiedPositions(Vector2I testGridPos) => GetOccupiedPositions(Item.ClearancePositions, testGridPos, MeshInstance.Rotation.Z);
        public HashSet<Vector2I> GetOccupiedPositions(float testRotZ) => GetOccupiedPositions(Item.ClearancePositions, GridPosition, testRotZ);

        public void SetGridPosition(Vector2I gridPos)
        {
            GridPosition = gridPos;
            MeshInstance.Position = GetPosFromGridPos(gridPos);
        }
    }

    private const float GridSpace = 0.175f;
    private const float GridThickness = 0.01f;

    private static readonly StringName _toggleName = "inventory_toggle";
    private static readonly StringName _leftName = "inventory_left", _rightName = "inventory_right", _upName = "inventory_up", _downName = "inventory_down";
    private static readonly StringName _useName = "inventory_use", _dropName = "inventory_drop", _moveName = "inventory_move", _rotateName = "inventory_rotate";

    private static readonly Vector2I _gridSize = new(6, 4);
    private static readonly Vector2 _bottomLeftPos = new
    (
        _gridSize.X * (GridSpace + GridThickness) * -0.5f,
        _gridSize.Y * (GridSpace + GridThickness) * -0.5f
    );
    private static readonly QuadMesh _selectorMesh = new() { Size = Vector2.One * GridSpace };

    public event Action Opened;
    public event Action Closed;
    public event Action<Item> ItemRemoved;

    private readonly Dictionary<Item, GridData> _itemToGridData = new();
    private readonly Dictionary<Vector2I, GridData> _gridPosToGridData = new();
    private readonly HashSet<Vector2I> _emptyPosS = new();

    private GridData _selectedGridData;

    // Selector //
    private MeshInstance3D _selectorMeshInst;
    private OrmMaterial3D _selectorMaterial;
    private Vector2I _selectorGridPos;

    // Used To Cancel Move, Then Revert //
    private Vector2I _oldGridPos;
    private Vector3 _oldRotation;

    public static Vector3 GetPosFromGridPos(Vector2I gridPos)
    {
        return new
        (
            _bottomLeftPos.X + (GridThickness * 0.5f) + ((gridPos.X + 0.5f) * (GridSpace + GridThickness)),
            _bottomLeftPos.Y + (GridThickness * 0.5f) + ((gridPos.Y + 0.5f) * (GridSpace + GridThickness)),
            0f
        );
    }

    public Inventory() 
    {
        for (int y = 0; y < _gridSize.Y; y++)
        {
            for (int x = 0; x < _gridSize.X; x++)
            {
                _emptyPosS.Add(new(x, y));
            }
        }
    }

    public override void _Ready()
    {
        base._Ready();

        _selectorMaterial = new OrmMaterial3D()
        {
            NoDepthTest = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = Colors.Yellow
        };

        _selectorMeshInst = new()
        {
            Name = "Selector",
            Position = GetPosFromGridPos(Vector2I.Zero),
            MaterialOverride = _selectorMaterial,
            Mesh = _selectorMesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_selectorMeshInst);

        CreateGrid();
        TryAddItem(new());
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);

        if (Console.Inst.IsOpen || FreeCameraController.Inst.Current) { return; }

        if (@event.IsActionPressed(_toggleName))
        {
            Visible = !Visible;
            CancelMove();
            // TODO: Play animation

            if (Visible) { Opened?.Invoke(); }
            else         { Closed?.Invoke(); }
        }

        if (!Visible) { return; }

        if      (@event.IsActionPressed(_leftName))   { MoveSelection(Vector2I.Left); }
        else if (@event.IsActionPressed(_rightName))  { MoveSelection(Vector2I.Right); }
        else if (@event.IsActionPressed(_upName))     { MoveSelection(Vector2I.Down); }
        else if (@event.IsActionPressed(_downName))   { MoveSelection(Vector2I.Up); }
        else if (@event.IsActionPressed(_moveName))   { ToggleMove(); }
        else if (@event.IsActionPressed(_rotateName)) { Rotate(); }
        else if (@event.IsActionPressed(_dropName))   { CancelMove(); }
    }

    public bool TryAddItem(Item item)
    {
        (Vector2I, float, HashSet<Vector2I>)? gridPos_rotZ_occupiedPosS = FindValidPositioning(item.ClearancePositions);
        if (!gridPos_rotZ_occupiedPosS.HasValue) { return false; }

        (Vector2I gridPos, float rotZ, HashSet<Vector2I> occupiedPosS) = gridPos_rotZ_occupiedPosS.Value;

        GridData data = new(item, CreateItemMeshInstance(gridPos, rotZ), CreateSelectorMesh(item.ClearancePositions));
        data.SetGridPosition(gridPos);

        _itemToGridData.Add(item, data);
        foreach (Vector2I pos in occupiedPosS) { _gridPosToGridData.Add(pos, data); }
        _emptyPosS.ExceptWith(occupiedPosS);

        return true;
    }
    public void RemoveItem(Item item)
    {

        ItemRemoved?.Invoke(item);
    }

    private void SetSelectorGridPosition(Vector2I gridPos)
    {
        _selectorMeshInst.Position = GetPosFromGridPos(gridPos);
        _selectorGridPos = gridPos;
    }

    private MeshInstance3D CreateGrid()
    {
        MeshInstance3D meshInst = new()
        {
            Name = "Grid",
            Position = new(_bottomLeftPos.X, _bottomLeftPos.Y, 0f),
            Mesh = new GridMesh(_gridSize, GridSpace, GridThickness)
            {
                Material = new OrmMaterial3D()
                {
                    AlbedoColor = Colors.Green,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    NoDepthTest = true,
                    RenderPriority = 2
                }
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(meshInst);

        return meshInst;
    }

    private MeshInstance3D CreateItemMeshInstance(Vector2I gridPos, float rotationZ)
    {
        MeshInstance3D meshInst = new()
        {
            Position = GetPosFromGridPos(gridPos),
            Rotation = Vector3.Back * rotationZ,
            Mesh = new BoxMesh()
            {
                Material = new OrmMaterial3D()
                {
                    AlbedoColor = Colors.Purple,
                    NoDepthTest = true,
                    RenderPriority = 1
                },
                Size = new(0.05f, 0.25f, 0.05f)
            },
        };
        AddChild(meshInst);

        return meshInst;
    }

    private ArrayMesh CreateSelectorMesh(Vector2I[] clearancePosS)
    {
        SurfaceTool st = new();

        st.Begin(Mesh.PrimitiveType.Triangles);

        Vector3 spaceUp = Vector3.Up * GridSpace;
        Vector3 spaceRight = Vector3.Right * GridSpace;

        foreach (Vector2I pos in clearancePosS)
        {
            Vector3 bottomLeftPos = new Vector3
            (
                (GridThickness * 0.5f) + ((pos.X - 0.5f) * (GridSpace + GridThickness)),
                (GridThickness * 0.5f) + ((pos.Y - 0.5f) * (GridSpace + GridThickness)),
                0f
            );
            Vector3 topLeftPos = bottomLeftPos + spaceUp;
            Vector3 bottomRightPos = bottomLeftPos + spaceRight;

            // Bottom-Left Triangle //
            st.AddVertex(bottomLeftPos);
            st.AddVertex(topLeftPos);
            st.AddVertex(bottomRightPos);

            // Top-Right Triangle //
            st.AddVertex(topLeftPos + spaceRight);
            st.AddVertex(bottomRightPos);
            st.AddVertex(topLeftPos);
        }

        return st.Commit();
    }

    /// <summary>
    /// Moves <see cref="_selectorGridPos"/> and the <see cref="_selectedGridData"/>.Position depending on <see cref="_selectedGridData"/>.
    /// </summary>
    private void MoveSelection(Vector2I move)
    {
        Vector2I nextGridPos = _selectorGridPos + move;
        if (_selectedGridData == null)
        {
            if (!WithinBounds(nextGridPos)) { return; }
        }
        else
        {
            HashSet<Vector2I> occupiedPosS = _selectedGridData.GetOccupiedPositions(nextGridPos);
            foreach (Vector2I pos in occupiedPosS)
            {
                if (!WithinBounds(pos)) { return; }
            }
            _selectedGridData.SetGridPosition(nextGridPos);
        }
        SetSelectorGridPosition(nextGridPos);
    }

    private void ToggleMove()
    {
        if (_selectedGridData == null)
        {
            if (!_gridPosToGridData.TryGetValue(_selectorGridPos, out GridData data)) { return; }
            
            _selectedGridData = data;

            HashSet<Vector2I> occupiedPositions = _selectedGridData.GetOccupiedPositions();
            foreach (Vector2I pos in occupiedPositions) { _gridPosToGridData.Remove(pos); }
            _emptyPosS.UnionWith(_selectedGridData.GetOccupiedPositions());

            SetSelectorGridPosition(data.GridPosition);
            _selectorMeshInst.Rotation = data.MeshInstance.Rotation;
            _selectorMeshInst.Mesh = data.SelectionMesh;
            _selectorMaterial.AlbedoColor = Colors.White;

            _oldGridPos = data.GridPosition;
            _oldRotation = data.MeshInstance.Rotation;
        }
        else
        {
            HashSet<Vector2I> occupiedPositions = _selectedGridData.GetOccupiedPositions();
            if (!occupiedPositions.IsSubsetOf(_emptyPosS)) { return; }

            foreach (Vector2I pos in occupiedPositions) { _gridPosToGridData.Add(pos, _selectedGridData); }
            _emptyPosS.ExceptWith(occupiedPositions);
            _selectedGridData = null;

            _selectorMeshInst.Mesh = _selectorMesh;
            _selectorMaterial.AlbedoColor = Colors.Yellow;
        }
    }

    private void RevertMove()
    {
        _selectedGridData.SetGridPosition(_oldGridPos);
        _selectedGridData.MeshInstance.Rotation = _oldRotation;

        SetSelectorGridPosition(_oldGridPos);
        _selectorMeshInst.Rotation = _oldRotation;
    }

    private void CancelMove()
    {
        if (_selectedGridData == null) { return; }

        RevertMove();
        ToggleMove();
    }

    private void Rotate()
    {
        if (_selectedGridData == null) { return; }

        // 90 Degrees //
        Vector3 rotation = Vector3.Back * (_selectedGridData.MeshInstance.Rotation.Z - (Mathf.Pi * 0.5f));
        if (TryMoveBackIntoGrid(_selectedGridData.GetOccupiedPositions(rotation.Z)))
        {
            _selectorMeshInst.Rotation = rotation;
            _selectedGridData.MeshInstance.Rotation = rotation;

            return;
        }

        // 180 Degrees (should always work, given it takes up the same space along each axis when not rotated) //
        rotation = Vector3.Back * (_selectedGridData.MeshInstance.Rotation.Z - Mathf.Pi);

        _selectorMeshInst.Rotation = rotation;
        _selectedGridData.MeshInstance.Rotation = rotation;

        TryMoveBackIntoGrid(_selectedGridData.GetOccupiedPositions());
    }

    /// <returns>Successfully in a valid position.</returns>
    private bool TryMoveBackIntoGrid(HashSet<Vector2I> occupiedPosS)
    {
        int lowestX = int.MaxValue, highestX = int.MinValue;
        int lowestY = int.MaxValue, highestY = int.MinValue;

        foreach (Vector2I pos in occupiedPosS)
        {
            if (pos.X < lowestX) { lowestX = pos.X; }
            if (pos.X > highestX) { highestX = pos.X; }

            if (pos.Y < lowestY) { lowestY = pos.Y; }
            if (pos.Y > highestY) { highestY = pos.Y; }
        }

        // Where It Exceeds The Grid //
        bool lowerX = lowestX < 0, upperX = highestX >= _gridSize.X;
        bool lowerY = lowestY < 0, upperY = highestY >= _gridSize.Y;
        //

        // Doesn't exceed any of the grid's bounds
        if (!(lowerX || upperX || lowerY || upperY)) { return true; }

        // Too large to fit grid
        if (highestX - lowestX >= _gridSize.X || highestY - lowestY >= _gridSize.Y) { return false; }

        Vector2I newPos = _selectedGridData.GridPosition;

        if (lowerX)      { newPos += Vector2I.Left * lowestX; }
        else if (upperX) { newPos += Vector2I.Left * (highestX - (_gridSize.X - 1)); }

        if (lowerY)      { newPos += Vector2I.Up * lowestY; }
        else if (upperY) { newPos += Vector2I.Up * (highestY - (_gridSize.Y - 1)); }

        SetSelectorGridPosition(newPos);
        _selectedGridData.SetGridPosition(newPos);

        return true;
    }

    private bool WithinBounds(Vector2I gridPos) => (gridPos.X >= 0 && gridPos.X < _gridSize.X) && (gridPos.Y >= 0 && gridPos.Y < _gridSize.Y);

    /// <returns>(GridPosition, RotationZ, OccupiedPositions)</returns>
    private (Vector2I, float, HashSet<Vector2I>)? FindValidPositioning(Vector2I[] clearancePosS)
    {
        for (int y = 0; y < _gridSize.Y; y++)
        {
            for (int x = 0; x < _gridSize.X; x++)
            {
                Vector2I gridPos = new(x, y);
                HashSet<Vector2I> occupiedPositions;

                occupiedPositions = GridData.GetOccupiedPositions(clearancePosS, gridPos, 0f);
                if (occupiedPositions.IsSubsetOf(_emptyPosS)) { return (gridPos, 0f, occupiedPositions); }

                const float Pi05 = Mathf.Pi * 0.5f;
                occupiedPositions = GridData.GetOccupiedPositions(clearancePosS, gridPos, Pi05);
                if (occupiedPositions.IsSubsetOf(_emptyPosS)) { return (gridPos, Pi05, occupiedPositions); }

                occupiedPositions = GridData.GetOccupiedPositions(clearancePosS, gridPos, Mathf.Pi);
                if (occupiedPositions.IsSubsetOf(_emptyPosS)) { return (gridPos, Mathf.Pi, occupiedPositions); }

                const float Pi15 = Mathf.Pi * 1.5f;
                occupiedPositions = GridData.GetOccupiedPositions(clearancePosS, gridPos, Pi15);
                if (occupiedPositions.IsSubsetOf(_emptyPosS)) { return (gridPos, Pi15, occupiedPositions); }
            }
        }
        return null;
    }
}
