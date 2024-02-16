#if DEBUG
using Godot;
using System;
using System.Collections.Generic;

namespace Scripts;

public static class Debug
{
    private static readonly List<MeshInstance3D> s_meshInstances = new();

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

    public static void CreatePoint(Node parent, Color colour, Vector3 pos, float radius = 0.01f)
    {
        (MeshInstance3D meshInst, OrmMaterial3D material) = GetNewMeshInstAndMaterial(parent, colour, pos);

        SphereMesh mesh = new()
        {
            Radius = radius,
            Height = radius * 2f,
            Material = material,
        };
        meshInst.Mesh = mesh;
    }

    /// <summary>
    /// Queue free all debug mesh instances.
    /// </summary>
    public static void Clear()
    {
        foreach (MeshInstance3D inst in s_meshInstances) { inst.QueueFree(); }
        s_meshInstances.Clear();
    }

    private static (MeshInstance3D, OrmMaterial3D) GetNewMeshInstAndMaterial(Node parent, Color colour, Vector3 pos)
    {
        MeshInstance3D meshInst = new()
        {
            Position = pos,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        parent.AddChild(meshInst);
        s_meshInstances.Add(meshInst);

        OrmMaterial3D material = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = colour
        };

        return (meshInst, material);
    }
}
#endif
