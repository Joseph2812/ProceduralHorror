#if DEBUG
using Godot;
using System;
using System.Collections.Generic;

namespace Scripts;

public static class Debug
{
    private static readonly List<MeshInstance3D> _meshInstances = new();

    public static void CreateBox(Node parent, Color colour, Vector3 centrePos, Vector3 size)
    {
        (MeshInstance3D meshInst, OrmMaterial3D material) = GetNewMeshInstAndMaterial(parent, colour, centrePos);

        BoxMesh mesh = new()
        {
            Size = size,
            Material = material
        };
        meshInst.Mesh = mesh;
    }

    public static void CreateLine(Node parent, Color colour, Vector3 startPos, Vector3 endPos)
    {
        (MeshInstance3D meshInst, OrmMaterial3D material) = GetNewMeshInstAndMaterial(parent, colour, startPos);

        ImmediateMesh mesh = new();
        meshInst.Mesh = mesh;

        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
        mesh.SurfaceAddVertex(Vector3.Zero);
        mesh.SurfaceAddVertex(endPos - startPos);
        mesh.SurfaceEnd();
    }

    public static void CreatePoint(Node parent, Color colour, Vector3 pos)
    {
        (MeshInstance3D meshInst, OrmMaterial3D material) = GetNewMeshInstAndMaterial(parent, colour, pos);

        const float Radius = 0.05f;
        SphereMesh mesh = new()
        {
            Radius = Radius,
            Height = Radius * 2f,
            Material = material,
        };
        meshInst.Mesh = mesh;
    }

    /// <summary>
    /// Queue free all debug mesh instances.
    /// </summary>
    public static void Clear()
    {
        foreach (MeshInstance3D inst in _meshInstances) { inst.QueueFree(); }
        _meshInstances.Clear();
    }

    private static (MeshInstance3D, OrmMaterial3D) GetNewMeshInstAndMaterial(Node parent, Color colour, Vector3 pos)
    {
        MeshInstance3D meshInst = new()
        {
            Position = pos,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        parent.AddChild(meshInst);
        _meshInstances.Add(meshInst);

        OrmMaterial3D material = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = colour
        };

        return (meshInst, material);
    }
}
#endif
