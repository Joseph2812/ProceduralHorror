using Godot;
using System;
using System.Collections.Generic;

namespace Scripts.Generation;

public class ItemManager
{
    private const string CubeTexturePath = "res://Textures/Cubes/";
    private const string CubeShaderPath = "res://Shaders/MixedCube.gdshader";
    private const int PureItemCount = 8;

    public enum Id
    {
        Empty = -1,

        // Pure Items //
        White,
        Orange,
        Red,
        Green,
        Blue,
        KitchenTiles,
        Wallpaper,
        Plaster,

        // Mixed Items //
        WhiteOrange,
        WhiteRed,
        WhiteGreen,
        WhiteBlue,
        WhiteKitchenTiles,
        WhiteWallpaper,
        WhitePlaster,

        OrangeRed,
        OrangeGreen,
        OrangeBlue,
        OrangeKitchenTiles,
        OrangeWallpaper,
        OrangePlaster,

        RedGreen,
        RedBlue,
        RedKitchenTiles,
        RedWallpaper,
        RedPlaster,

        GreenBlue,
        GreenKitchenTiles,
        GreenWallpaper,
        GreenPlaster,

        BlueKitchenTiles,
        BlueWallpaper,
        BluePlaster,

        KitchenTilesWallpaper,
        KitchenTilesPlaster,

        WallpaperPlaster
    }

    private class MixedItemIdComparer : EqualityComparer<(Id, Id)>
    {
        public override bool Equals((Id, Id) x, (Id, Id) y)
        {
            return (x.Item1 == y.Item1) && (x.Item2 == y.Item2) ||
                   (x.Item1 == y.Item2) && (x.Item2 == y.Item1);
        }

        public override int GetHashCode((Id, Id) obj) => ((int)obj.Item1) ^ ((int)obj.Item2);
    }

    private readonly Dictionary<(Id, Id), Id> _mixedItems = new(new MixedItemIdComparer());
    private readonly Dictionary<Id, (Id, Id)> _reverseMixedItems = new();

    public ItemManager()
    {
        for (int i = 0; i < PureItemCount; i++)
        {
            for (int j = i + 1; j < PureItemCount; j++)
            {
                Id id1 = (Id)i;
                Id id2 = (Id)j;

                _mixedItems.Add
                (
                    (id1, id2),
                    (Id)Enum.Parse(typeof(Id), Enum.GetName(typeof(Id), id1) + Enum.GetName(typeof(Id), id2))
                );
            }
        }

        // Create Reverse Lookup //
        foreach (KeyValuePair<(Id, Id), Id> pair in _mixedItems)
        {
            _reverseMixedItems.Add(pair.Value, pair.Key);
        }
    }

    /// <summary>
    /// Create a <see cref="MeshLibrary"/> from <see cref="Id"/> constants, and matching textures in the resources folder.
    /// </summary>
    /// <returns><see cref="MeshLibrary"/> containing all generated <see cref="BoxMesh"/>es.</returns>
    public MeshLibrary GetMeshLibrary()
    {
        MeshLibrary lib = new();
        Shader shader = GD.Load<Shader>(CubeShaderPath);
        int mixedIdx = PureItemCount;

        // Pure Cube //
        for (int i = 0; i < PureItemCount; i++)
        {
            StandardMaterial3D mat = new StandardMaterial3D() { Uv1Scale = new(3f, 2f, 1f) };
            BoxMesh box = new() { Material = mat };

            string itemPath = CubeTexturePath + Enum.GetName(typeof(Id), i);

            // Setup Standard Material //
            if (TryLoadTexture($"{itemPath}/AlbedoMap", out Texture2D texture)) { mat.AlbedoTexture = texture; }
            if (TryLoadTexture($"{itemPath}/MetallicMap", out texture))         { mat.MetallicTexture = texture; }
            if (TryLoadTexture($"{itemPath}/RoughnessMap", out texture))        { mat.RoughnessTexture = texture; }

            if (TryLoadTexture($"{itemPath}/NormalMap", out texture))
            {
                mat.NormalEnabled = true;
                mat.NormalTexture = texture;
            }
            if (TryLoadTexture($"{itemPath}/AmbientOcclusionMap", out texture))
            {
                mat.AOEnabled = true;
                mat.AOTexture = texture;
            }
            //

            lib.CreateItem(i);
            lib.SetItemMesh(i, box);

            // Mixed Items //
            for (int j = i + 1; j < PureItemCount; j++)
            {
                ShaderMaterial shaderMat = new ShaderMaterial() { Shader = shader };
                box = new() { Material = shaderMat };

                shaderMat.SetShaderParameter($"albedoMap1"          , mat.AlbedoTexture);
                shaderMat.SetShaderParameter($"metallicMap1"        , mat.MetallicTexture);
                shaderMat.SetShaderParameter($"roughnessMap1"       , mat.RoughnessTexture);
                shaderMat.SetShaderParameter($"normalMap1"          , mat.NormalTexture);
                shaderMat.SetShaderParameter($"ambientOcclusionMap1", mat.AOTexture);

                // Load 2nd Textures //
                itemPath = CubeTexturePath + Enum.GetName(typeof(Id), j);
                if (TryLoadTexture($"{itemPath}/AlbedoMap", out texture))
                {
                    shaderMat.SetShaderParameter($"albedoMap2", texture);
                }

                if (TryLoadTexture($"{itemPath}/MetallicMap", out texture))
                {
                    shaderMat.SetShaderParameter($"metallicMap2", texture);
                }

                if (TryLoadTexture($"{itemPath}/RoughnessMap", out texture))
                {
                    shaderMat.SetShaderParameter($"roughnessMap2", texture);
                }

                if (TryLoadTexture($"{itemPath}/NormalMap", out texture))
                {
                    shaderMat.SetShaderParameter($"normalMap2", texture);
                }

                if (TryLoadTexture($"{itemPath}/AmbientOcclusionMap", out texture))
                {
                    shaderMat.SetShaderParameter($"ambientOcclusionMap2", texture);
                }
                //

                lib.CreateItem(mixedIdx);
                lib.SetItemMesh(mixedIdx, box);
                mixedIdx++;
            }
        }
        return lib;
    }
    private bool TryLoadTexture(string fullPathWithoutExt, out Texture2D texture)
    {
        string pathTres = fullPathWithoutExt + ".tres";
        string pathJpg = fullPathWithoutExt + ".jpg";

        texture = null;
        if (ResourceLoader.Exists(pathTres))     { texture = GD.Load<Texture2D>(pathTres); }
        else if (ResourceLoader.Exists(pathJpg)) { texture = GD.Load<Texture2D>(pathJpg); }
        else                                     { return false; }

        return true;
    }


    /// <summary>
    /// Find the mixed equivalent of two item IDs, and whether it matches the order in the stored dictionary (<paramref name="mixedId"/> = <paramref name="id2"/> on failure).
    /// </summary>
    /// <param name="id1">1st item ID.</param>
    /// <param name="id2">2nd item ID (<paramref name="mixedId"/> set as this if it fails).</param>
    /// <param name="mixedId">The id mix between <paramref name="id1"/> and <paramref name="id2"/>, if it fails it's just <paramref name="id2"/>.</param>
    /// <param name="reversed">Reversed if it doesn't match the order stored in the dictionary.</param>
    /// <returns>Whether it was a success (failure if one the IDs is already mixed, or IDs are the same).</returns>
    public bool GetMixedId(Id id1, Id id2, out Id mixedId, out bool reversed)
    {      
        if (_mixedItems.TryGetValue((id1, id2), out mixedId))
        {
            reversed = id1 != _reverseMixedItems[mixedId].Item1;
            return true;
        }
        else
        {
            mixedId = id2;
            reversed = false;
            return false;
        }
    }
}