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

    /// <summary>
    /// Remove the end directory (or file) of a path. Essentially, going back a directory.
    /// </summary>
    /// <param name="path">Path with forward slashes. Works with or without an ending forward slash.</param>
    /// <returns>Path ending in a forward slash (e.g. <c>this/a/path/</c> -> <c>this/a/</c>).</returns>
    public static string GetPathWithoutEndDirectory(string path)
    {
        int i;
        for (i = path.Length - 2; i >= 0; i--) // Skip last path element as it could be a forward slash
        {
            if (path[i] == '/') { break; }
        }
        return path[..(i + 1)];
    }

    /// <summary>
    /// Loads sub-directory located outside of the resource path.<para/>
    /// Don't use this if loading multiple sub-directories in the same main directory.<br/>Since <see cref="GetPathWithoutEndDirectory(string)"/> will be called more times than needed, and you should cache the result for reuse.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="resourcePath">Path of resource.</param>
    /// <param name="subDir">Sub-directory that is one up from the <paramref name="resourcePath"/> with forward slash (e.g. <c>SubDir/</c>).</param>
    /// <returns>Resources loaded from the sub-directory.</returns>
    public static T[] LoadSubDirectoryUpFromResource<T>(string resourcePath, string subDir) where T : Resource
    {
        string fullPath = GetPathWithoutEndDirectory(resourcePath) + subDir;
        return LoadPaths<T>
        (
            DirAccess.GetFilesAt(fullPath),
            fullPath
        );
    }
}
