using Godot;
using System;

namespace Scripts;

public static class CommonMethods
{
    public static Godot.Collections.Dictionary GetCategory(string name)
    {
        return new Godot.Collections.Dictionary
        {
            { "name", name },
            { "type", (int)Variant.Type.Nil },
            { "usage", (int)PropertyUsageFlags.Category }
        };
    }
    public static Godot.Collections.Dictionary GetGroup(string name)
    {
        return new Godot.Collections.Dictionary
        {
            { "name", name },
            { "type", (int)Variant.Type.Nil },
            { "usage", (int)PropertyUsageFlags.Group }
        };
    }

    public static T[] LoadPaths<T>(string[] paths, string prefix = "") where T : Resource
    {
        T[] resources = new T[paths.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            resources[i] = GD.Load<T>(prefix + paths[i]);
        }
        return resources;
    }
}
