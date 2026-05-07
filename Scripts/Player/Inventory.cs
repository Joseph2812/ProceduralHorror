using Godot;
using System;
using System.Collections.Generic;
using Scripts.Pickups;
using Scripts.Extensions;

namespace Scripts.Player;

public partial class Inventory : Node3D
{
    private partial class GridData(Pickup pickup, Node3D pivot, MeshInstance3D meshInst, Material material, ArrayMesh selectionMesh, Label3D equippedLabel, Label3D hotkeyLabel) : GodotObject
    {
        public readonly Pickup Pickup = pickup;
        public readonly Node3D Pivot = pivot;
        public readonly MeshInstance3D MeshInstance = meshInst;
        public readonly Material Material = material;
        public readonly ArrayMesh SelectionMesh = selectionMesh;
        public readonly Label3D EquippedLabel = equippedLabel;
        public readonly Label3D HotkeyLabel = hotkeyLabel;

        public Vector2I GridPosition { get; private set; }

        public static HashSet<Vector2I> GetOccupiedPositions(Godot.Collections.Array<Vector2I> clearancePosS, Vector2I gridPos, float rotationZ)
        {
            HashSet<Vector2I> positions = new(clearancePosS.Count);
            foreach (Vector2I pos in clearancePosS)
            {
                positions.Add(gridPos + pos.RotatedZ(rotationZ));
            }
            return positions;
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what != NotificationPredelete) { return; }
            
            Pivot.QueueFree();
            EquippedLabel.QueueFree();
            HotkeyLabel.QueueFree();
        }

        public HashSet<Vector2I> GetOccupiedPositions() => GetOccupiedPositions(Pickup.ClearancePositions, GridPosition, Pivot.Rotation.Z);
        public HashSet<Vector2I> GetOccupiedPositions(Vector2I testGridPos) => GetOccupiedPositions(Pickup.ClearancePositions, testGridPos, Pivot.Rotation.Z);
        public HashSet<Vector2I> GetOccupiedPositions(float testRotZ) => GetOccupiedPositions(Pickup.ClearancePositions, GridPosition, testRotZ);

        public void SetGridPosition(Vector2I gridPos)
        {
            Vector3 pos = GetCentrePosFromGridPos(gridPos);

            GridPosition = gridPos;
            Pivot.Position = pos;

            UpdateLabelPosition(pos);
        }

        /// <summary>
        /// NOTE: Call deferred when changing label contents, so AABB can update first.
        /// </summary>
        public void UpdateLabelPosition() => UpdateLabelPosition(GetCentrePosFromGridPos(GridPosition));
        private void UpdateLabelPosition(Vector3 centrePos)
        {
            const float HalfGridSpace = GridSpace * 0.5f;
            Vector3 equippedLabelSize = EquippedLabel.GetAabb().Size;
            Vector3 hotkeyLabelSize = HotkeyLabel.GetAabb().Size;

            EquippedLabel.Position = new
            (
                centrePos.X + (equippedLabelSize.X * 0.5f) - HalfGridSpace,
                centrePos.Y + (equippedLabelSize.Y * 0.5f) - HalfGridSpace,
                centrePos.Z
            );
            HotkeyLabel.Position = new
            (
                centrePos.X + (hotkeyLabelSize.X * 0.5f) - HalfGridSpace,
                centrePos.Y - (hotkeyLabelSize.Y * 0.5f) + HalfGridSpace,
                centrePos.Z
            );
        }
    }

    private const float GridSpace = 0.15f;
    private const float GridThickness = 0.01f;
    private const int HotkeyCount = 4; // Numbered Hotkeys

    private static readonly StringName s_toggleName = "inventory_toggle";
    private static readonly StringName s_leftName = "inventory_left", s_rightName = "inventory_right", s_upName = "inventory_up", s_downName = "inventory_down";
    private static readonly StringName s_useName = "inventory_use", s_useAltName = "inventory_use_alt", s_dropName = "inventory_drop", s_moveName = "inventory_move", s_rotateName = "inventory_rotate";
    private static readonly StringName s_hotkey1Name = "hotkey_1", s_hotkey2Name = "hotkey_2", s_hotkey3Name = "hotkey_3", s_hotkey4Name = "hotkey_4";
    private static readonly StringName s_hotkeyAlt1Name = "hotkey_alt_1", s_hotkeyAlt2Name = "hotkey_alt_2", s_hotkeyAlt3Name = "hotkey_alt_3", s_hotkeyAlt4Name = "hotkey_alt_4";

    private static readonly Vector2I s_gridSize = new(6, 4);
    private static readonly Vector3 s_gridOneUp = Vector3.Up * GridSpace;
    private static readonly Vector3 s_gridOneRight = Vector3.Right * GridSpace;

    /// <summary>
    /// Applied to the grid MeshInstance to centre it on the screen.
    /// </summary>
    private static readonly Vector3 s_offset = new
    (
        s_gridSize.X * (GridSpace + GridThickness) * -0.5f,
        s_gridSize.Y * (GridSpace + GridThickness) * -0.5f,
        0f
    );
    private static readonly QuadMesh s_selectorMesh = new() { Size = Vector2.One * GridSpace };
    private static Node s_sceneRoot;

    public event Action Opened, Closed;
    public event Action<Item> ItemRemoved;

    private GridData[] _assignedGridData = new GridData[HotkeyCount];

    private readonly Dictionary<Pickup, GridData> _pickupToGridData = [];
    private readonly Dictionary<Vector2I, GridData> _gridPosToGridData = [];
    private readonly HashSet<Vector2I> _emptyPosS = [];

    private GridData _selectedGridData;
    private ArmsManager _armsManager;

    // Selector //
    private MeshInstance3D _selectorMeshInst;
    private OrmMaterial3D _selectorMaterial;
    private Vector2I _selectorGridPos;

    // Used To Cancel Move, Then Revert //
    private Vector2I _oldGridPos;
    private Vector3 _oldRotation;

    /// <returns>Centre position of the grid square at <paramref name="gridPos"/>.</returns>
    public static Vector3 GetCentrePosFromGridPos(Vector2I gridPos)
    {
        return new
        (
            s_offset.X + (GridThickness * 0.5f) + ((gridPos.X + 0.5f) * (GridSpace + GridThickness)),
            s_offset.Y + (GridThickness * 0.5f) + ((gridPos.Y + 0.5f) * (GridSpace + GridThickness)),
            0f
        );
    }

    public Inventory() 
    {
        for (int y = 0; y < s_gridSize.Y; y++)
        {
            for (int x = 0; x < s_gridSize.X; x++)
            {
                _emptyPosS.Add(new(x, y));
            }
        }
    }

    public override void _Ready()
    {
        base._Ready();

        s_sceneRoot = GetNode("/root/Main");
        _armsManager = GetParent().GetNode<ArmsManager>("ArmsManager");

        _selectorMaterial = new OrmMaterial3D()
        {
            NoDepthTest = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = Colors.Yellow
        };

        _selectorMeshInst = new()
        {
            Name             = "Selector",
            Position         = GetCentrePosFromGridPos(Vector2I.Zero),
            Mesh             = s_selectorMesh,
            CastShadow       = GeometryInstance3D.ShadowCastingSetting.Off
        };
        s_selectorMesh.SurfaceSetMaterial(0, _selectorMaterial);
        AddChild(_selectorMeshInst);

        CreateGrid();
        Console.Inst.AddCommand
        (
            "give",
            new
            (
                OnCommand_Give,
                "[color=#aaa]<pickupName>, <amount?>[/color]. Add an pickup to your inventory. By default it adds one."
            )
        );

        _armsManager.ItemArmChanged += OnArmsManager_EquippedStateChanged;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);

        if (Console.Inst.IsOpen || FreeCameraController.Inst.Current) { return; }

        if (@event.IsActionPressed(s_toggleName))
        {
            Visible = !Visible;
            CancelMove();
            // TODO: Play animation

            if (Visible) { Opened?.Invoke(); }
            else         { Closed?.Invoke(); }
        }
        else if (@event.IsActionPressed(s_hotkey1Name, exactMatch: true)) { UseHotkey(0); }
        else if (@event.IsActionPressed(s_hotkey2Name, exactMatch: true)) { UseHotkey(1); }
        else if (@event.IsActionPressed(s_hotkey3Name, exactMatch: true)) { UseHotkey(2); }
        else if (@event.IsActionPressed(s_hotkey4Name, exactMatch: true)) { UseHotkey(3); }
        else if (@event.IsActionPressed(s_hotkeyAlt1Name)) { UseHotkeyAlt(0); }
        else if (@event.IsActionPressed(s_hotkeyAlt2Name)) { UseHotkeyAlt(1); }
        else if (@event.IsActionPressed(s_hotkeyAlt3Name)) { UseHotkeyAlt(2); }
        else if (@event.IsActionPressed(s_hotkeyAlt4Name)) { UseHotkeyAlt(3); }

        if (!Visible) { return; }

        // Inventory Only Controls //
        if      (@event.IsActionPressed(s_leftName))  { MoveSelection(Vector2I.Left); }
        else if (@event.IsActionPressed(s_rightName)) { MoveSelection(Vector2I.Right); }
        else if (@event.IsActionPressed(s_upName))    { MoveSelection(Vector2I.Down); }
        else if (@event.IsActionPressed(s_downName))  { MoveSelection(Vector2I.Up); }
        else if (@event.IsActionPressed(s_useName, exactMatch: true))
        {
            if (_selectedGridData != null || !_gridPosToGridData.TryGetValue(_selectorGridPos, out GridData data) || data.Pickup is not Item item) { return; }
            Equip(item);
        }
        else if (@event.IsActionPressed(s_useAltName))
        {
            if (_selectedGridData != null || !_gridPosToGridData.TryGetValue(_selectorGridPos, out GridData data) || data.Pickup is not Item item) { return; }
            EquipAlt(item);
        }
        else if (@event.IsActionPressed(s_dropName))
        {
            if (_selectedGridData == null) { RemovePickup(_gridPosToGridData[_selectorGridPos]); }
            else                           { CancelMove(); }       
        }
        else if (@event.IsActionPressed(s_moveName))   { ToggleMove(); }
        else if (@event.IsActionPressed(s_rotateName)) { Rotate(); }
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what != NotificationPredelete) { return; }

        foreach (GridData data in _pickupToGridData.Values) { data.Free(); }
    }

    public bool TryAddPickup(Pickup pickup)
    {
        (Vector2I, float, HashSet<Vector2I>)? gridPos_rotZ_occupiedPosS = FindValidPositioning(pickup.ClearancePositions);
        if (!gridPos_rotZ_occupiedPosS.HasValue) { return false; }

        pickup.GetParent()?.RemoveChild(pickup);
        _armsManager.AddChild(pickup);
        pickup.Visible = false;
        pickup.Freeze = true;

        pickup.AddCollisionExceptionWith(Player.Inst); // Temporarily prevents being pushed when adding pickups using the add-pickup command
        pickup.CallDeferred(PhysicsBody3D.MethodName.RemoveCollisionExceptionWith, Player.Inst);
        pickup.CollisionShape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);

        (Vector2I gridPos, float rotZ, HashSet<Vector2I> occupiedPosS) = gridPos_rotZ_occupiedPosS.Value;
        (Node3D pivot, MeshInstance3D meshInst) = CreatePickupMeshInstanceOnPivot(pickup, rotZ);

        GridData data = new
        (
            pickup,
            pivot,
            meshInst,
            meshInst.GetActiveMaterial(0),
            CreateSelectorMesh(pickup.ClearancePositions), 
            CreateLabel(),
            CreateLabel()
        );
        _pickupToGridData.Add(pickup, data);

        data.SetGridPosition(gridPos);
        RegisterGridDataSpace(data, occupiedPosS);

        return true;
    }
    public void RemovePickup(Pickup pickup) { RemovePickup(_pickupToGridData[pickup]); }
    private void RemovePickup(GridData gridData)
    {
        _pickupToGridData.Remove(gridData.Pickup);

        _armsManager.RemoveChild(gridData.Pickup);
        s_sceneRoot.AddChild(gridData.Pickup);

        DeregisterGridDataSpace(gridData.GetOccupiedPositions());

        // TODO: Drop pickup physically on ground
        if (gridData.Pickup is Item item) { ItemRemoved?.Invoke(item); }

        gridData.Free();
    }

    private void RegisterGridDataSpace(GridData data, HashSet<Vector2I> occupiedPosS)
    {
        foreach (Vector2I pos in occupiedPosS) { _gridPosToGridData.Add(pos, data); }
        _emptyPosS.ExceptWith(occupiedPosS);
    }
    private void DeregisterGridDataSpace(HashSet<Vector2I> occupiedPosS)
    {
        foreach (Vector2I pos in occupiedPosS) { _gridPosToGridData.Remove(pos); }
        _emptyPosS.UnionWith(occupiedPosS);
    }

    private void SetSelectorGridPosition(Vector2I gridPos)
    {
        _selectorMeshInst.Position = GetCentrePosFromGridPos(gridPos);
        _selectorGridPos = gridPos;
    }

    private void CreateGrid()
    {
        MeshInstance3D meshInst = new()
        {
            Name = "Grid",
            Position = s_offset,
            Mesh = new GridMesh(s_gridSize, GridSpace, GridThickness)
            {
                Material = new OrmMaterial3D()
                {
                    AlbedoColor = Colors.Green,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    NoDepthTest = true,
                    RenderPriority = 0
                }
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(meshInst);
    }

    /// <summary>
    /// Creates new <see cref="Node3D"/> with <see cref="MeshInstance3D"/> as a child (preserves offset), to use for moving and displaying an pickup in the grid.<para/>
    /// NOTE: Both mesh and material are duplicated for unique modification.
    /// </summary>
    /// <returns><see cref="Node3D"/> with a <see cref="MeshInstance3D"/> child, where the parent node will act as a pivot.</returns>
    private (Node3D, MeshInstance3D) CreatePickupMeshInstanceOnPivot(Pickup pickup, float rotationZ)
    {
        Vector2I invSize = pickup.GetInventorySize();
        float largestInvSide = Mathf.Max(invSize.X, invSize.Y);

        const float Padding = 0.02f;
        MeshInstance3D meshInst = new()
        {
            Mesh = (Mesh)pickup.MeshInstance.Mesh.Duplicate(),
            MaterialOverride = (Material)pickup.Material.Duplicate(),
            Position = new Vector3(invSize.X - 1, invSize.Y - 1, 0f) * (GridThickness + GridSpace) * 0.5f,
            Rotation = pickup.InventoryRotation,
            Scale = ((GridSpace + (GridSpace + GridThickness) * (largestInvSide - 1f) - Padding) * Vector3.One) / pickup.MeshInstance.Mesh.GetAabb().GetLongestAxisSize()
        };

        BaseMaterial3D mat = (BaseMaterial3D)meshInst.GetActiveMaterial(0);
        mat.NextPass = null;
        mat.NoDepthTest = true;
        mat.RenderPriority = 1;

        // Parenting //
        Node3D pivot = new() { Rotation = Vector3.Back * rotationZ };
        AddChild(pivot);

        pivot.AddChild(meshInst);
        //

        return (pivot, meshInst);
    }

    private ArrayMesh CreateSelectorMesh(Godot.Collections.Array<Vector2I> clearancePosS)
    {
        SurfaceTool st = new();
        st.Begin(Mesh.PrimitiveType.Triangles);

        foreach (Vector2I pos in clearancePosS)
        {
            Vector3 bottomLeftPos = new
            (
                (GridThickness * 0.5f) + ((pos.X - 0.5f) * (GridSpace + GridThickness)),
                (GridThickness * 0.5f) + ((pos.Y - 0.5f) * (GridSpace + GridThickness)),
                0f
            );

            Vector3 topLeftPos = bottomLeftPos + s_gridOneUp;
            Vector3 bottomRightPos = bottomLeftPos + s_gridOneRight;

            // Bottom-Left Triangle //
            st.AddVertex(bottomLeftPos);
            st.AddVertex(topLeftPos);
            st.AddVertex(bottomRightPos);

            // Top-Right Triangle //
            st.AddVertex(topLeftPos + s_gridOneRight);
            st.AddVertex(bottomRightPos);
            st.AddVertex(topLeftPos);
        }

        return st.Commit();
    }
    private Label3D CreateLabel()
    {
        Label3D label = new()
        {
            FontSize = 64,
            PixelSize = 0.00075f,
            OutlineSize = 0,
            DoubleSided = false,
            NoDepthTest = true,
            RenderPriority = 3,
            Visible = false
        };
        AddChild(label);

        return label;
    }

    private void UseHotkey(int idx)
    {
        GridData gridData = _assignedGridData[idx];

        if (Visible)               { Assign(idx); }
        else if (gridData != null) { Equip((Item)gridData.Pickup); }
    }
    private void UseHotkeyAlt(int idx)
    {
        GridData gridData = _assignedGridData[idx];
        if (!Visible && gridData != null) { EquipAlt((Item)gridData.Pickup); }
    }

    private void Assign(int idx)
    {
        if (!_gridPosToGridData.TryGetValue(_selectorGridPos, out GridData data)) { return; }
        if (data.Pickup is not Item) { return; }

        // Remove Old Assignment For GridData If It Exists //
        for (int i = 0; i < _assignedGridData.Length; i++)
        {
            if (_assignedGridData[i] == data)
            {
                _assignedGridData[i] = null;
                break;
            }
        }
        //

        GridData oldData = _assignedGridData[idx];
        if (oldData != null) { oldData.HotkeyLabel.Visible = false; }

        _assignedGridData[idx] = data;
        data.HotkeyLabel.Visible = true;
        data.HotkeyLabel.Text = GetHotkeyString(idx);

        data.CallDeferred(GridData.MethodName.UpdateLabelPosition);
    }
    private string GetHotkeyString(int idx)
    {
        return idx switch
        {
            0 => "1",
            1 => "2",
            2 => "3",
            3 => "4",
            _ => throw new ArgumentException($"Index: {idx}, is not a hotkey."),
        };
    }

    private void Equip(Item item)
    {
        if (item.TwoHanded) { _armsManager.EquipBoth(item); }
        else                { _armsManager.EquipRight(item); }
    }
    private void EquipAlt(Item item)
    {
        if (item.TwoHanded) { _armsManager.EquipBoth(item); }
        else                { _armsManager.EquipLeft(item); }
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
            DeregisterGridDataSpace(occupiedPositions);
            _selectedGridData.Material.RenderPriority = 2; // Render on top of everything (Except text)

            SetSelectorGridPosition(data.GridPosition);
            _selectorMeshInst.Rotation = data.Pivot.Rotation;
            _selectorMeshInst.Mesh = data.SelectionMesh;
            _selectorMaterial.AlbedoColor = Colors.White;

            _oldGridPos = data.GridPosition;
            _oldRotation = data.Pivot.Rotation;
        }
        else
        {
            HashSet<Vector2I> occupiedPositions = _selectedGridData.GetOccupiedPositions();
            if (!occupiedPositions.IsSubsetOf(_emptyPosS)) { return; }

            RegisterGridDataSpace(_selectedGridData, occupiedPositions);
            _selectedGridData.Material.RenderPriority = 1;
            _selectedGridData = null;

            _selectorMeshInst.Mesh = s_selectorMesh;
            _selectorMaterial.AlbedoColor = Colors.Yellow;
        }
    }
    private void RevertMove()
    {
        _selectedGridData.SetGridPosition(_oldGridPos);
        _selectedGridData.Pivot.Rotation = _oldRotation;

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
        Vector3 rotation = Vector3.Back * (_selectedGridData.Pivot.Rotation.Z - (Mathf.Pi * 0.5f));
        if (TryMoveBackIntoGrid(_selectedGridData.GetOccupiedPositions(rotation.Z)))
        {
            _selectorMeshInst.Rotation = rotation;
            _selectedGridData.Pivot.Rotation = rotation;

            return;
        }

        // 180 Degrees (should always work, given it takes up the same space along each axis when not rotated) //
        rotation = Vector3.Back * (_selectedGridData.Pivot.Rotation.Z - Mathf.Pi);

        _selectorMeshInst.Rotation = rotation;
        _selectedGridData.Pivot.Rotation = rotation;

        TryMoveBackIntoGrid(_selectedGridData.GetOccupiedPositions());
    }

    /// <returns>Success if in a valid position.</returns>
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
        bool lowerX = lowestX < 0, upperX = highestX >= s_gridSize.X;
        bool lowerY = lowestY < 0, upperY = highestY >= s_gridSize.Y;
        //

        // Doesn't exceed any of the grid's bounds
        if (!(lowerX || upperX || lowerY || upperY)) { return true; }

        // Too large to fit grid
        if (highestX - lowestX >= s_gridSize.X || highestY - lowestY >= s_gridSize.Y) { return false; }

        Vector2I newPos = _selectedGridData.GridPosition;

        if (lowerX)      { newPos += Vector2I.Left * lowestX; }
        else if (upperX) { newPos += Vector2I.Left * (highestX - (s_gridSize.X - 1)); }

        if (lowerY)      { newPos += Vector2I.Up * lowestY; }
        else if (upperY) { newPos += Vector2I.Up * (highestY - (s_gridSize.Y - 1)); }

        SetSelectorGridPosition(newPos);
        _selectedGridData.SetGridPosition(newPos);

        return true;
    }

    private bool WithinBounds(Vector2I gridPos) => (gridPos.X >= 0 && gridPos.X < s_gridSize.X) && (gridPos.Y >= 0 && gridPos.Y < s_gridSize.Y);

    /// <returns>(GridPosition, RotationZ, OccupiedPositions)</returns>
    private (Vector2I, float, HashSet<Vector2I>)? FindValidPositioning(Godot.Collections.Array<Vector2I> clearancePosS)
    {
        for (int y = 0; y < s_gridSize.Y; y++)
        {
            for (int x = 0; x < s_gridSize.X; x++)
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

    private void OnArmsManager_EquippedStateChanged(Item item, ArmsManager.Arm arm)
    {
        GridData data = _pickupToGridData[item];
        switch (arm)
        {
            case ArmsManager.Arm.None:
                data.EquippedLabel.Visible = false;
                break;

            case ArmsManager.Arm.Left:
                data.EquippedLabel.Visible = true;
                data.EquippedLabel.Text = "L";
                data.CallDeferred(GridData.MethodName.UpdateLabelPosition);
                break;

            case ArmsManager.Arm.Right:
                data.EquippedLabel.Visible = true;
                data.EquippedLabel.Text = "R";
                data.CallDeferred(GridData.MethodName.UpdateLabelPosition);
                break;

            case ArmsManager.Arm.Both:
                data.EquippedLabel.Visible = true;
                data.EquippedLabel.Text = "B";
                data.CallDeferred(GridData.MethodName.UpdateLabelPosition);
                break;

            default:
                throw new ArgumentException("Invalid arm.");
        }
    }

    private void OnCommand_Give(string[] commandSplit)
    {
        // Check Arguments //
        if (commandSplit.Length < 2)
        {
            Console.Inst.AppendLine($"An pickup name must be specified.");
            return;
        }

        string pickupName = commandSplit[1];
        string pickupPath = $"res://Scenes/Pickups/Items/{pickupName}.tscn";
        if (!FileAccess.FileExists(pickupPath))
        {
            pickupPath = $"res://Scenes/Pickups/Junk/{pickupName}.tscn";
            if (!FileAccess.FileExists(pickupPath))
            {
                Console.Inst.AppendLine($"No pickup exists with the name: \"{pickupName}\".");
                return;
            }
        }

        if (!(commandSplit.Length > 2 && int.TryParse(commandSplit[2], out int amount))) { amount = 1; }
        if (amount <= 0) { return; }

        // Add Pickups //
        PackedScene pickupScene = ResourceLoader.Load<PackedScene>(pickupPath);
        int pickupsAdded = 0;
        for (int i = 0; i < amount; i++)
        {
            Pickup pickup = pickupScene.Instantiate<Pickup>();
            if (!TryAddPickup(pickup))
            {
                pickup.QueueFree();
                break;
            }
            pickupsAdded++;
        }

        if      (pickupsAdded == 0) { Console.Inst.AppendLine($"No viable space was found for the \"{pickupName}\"."); }
        else if (pickupsAdded == 1) { Console.Inst.AppendLine($"Successfully added \"{pickupName}\" to your inventory!"); }
        else                      { Console.Inst.AppendLine($"Successfully added {pickupsAdded} \"{pickupName}\"s to your inventory!"); }
    }
}
