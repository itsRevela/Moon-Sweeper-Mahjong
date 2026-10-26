using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MoonMahjong.Core;
using MoonMahjong.Table.Layout;
using MoonMahjong.Table.Presentation;
using UnityEngine;

namespace MoonMahjong.Plugin;

// Run-time art: NaoMahjong's tile mesh and textures (prepared by scripts/prepare_assets.py) drawn with the game's own
// shader, copied from a material already in the scene, so lighting matches and no shader has to ship with the mod.
internal sealed class MahjongAssets
{
    // A tile is 26 mm wide at scale 1; height and thickness follow OpenRiichi's tile (TableLayout.TileSize, about
    // 26 : 33.5 : 18.9) so tiles fit its layout spacing exactly.
    private const float RealTileWidth = 0.026f;

    // Runtime-made meshes, materials and textures that no scene object uses yet are destroyed by the
    // Resources.UnloadUnusedAssets a scene load runs (our C# references don't count); the tile assets wait unused
    // until the first deal, so every asset here opts out.
    private const HideFlags KeepLoaded = HideFlags.DontUnloadUnusedAsset;

    private readonly Dictionary<string, Material> tileMaterials = new();
    private readonly Dictionary<string, Material> handTileMaterials = new();
    private readonly Dictionary<string, Material> highlightedTileMaterials = new();
    private readonly Dictionary<string, Material> dimmedTileMaterials = new();
    private readonly Dictionary<string, Texture2D> tileTextures = new();
    private readonly Dictionary<TableSound, AudioClip> sounds = new();
    private readonly Material template;

    private MahjongAssets(string directory, Material template, float scale)
    {
        this.template = template;
        var proportions = TableLayout.TileSize;
        TileSize = new Vector3(proportions.X, proportions.Y, proportions.Z) * (RealTileWidth * scale);
        TileMesh = LoadMesh(Path.Combine(directory, "tile_mesh.json"), "tile", TileSize);
        foreach (var name in AllTileNames())
        {
            var texture = LoadTexture(Path.Combine(directory, "tiles", name + ".png"));
            tileTextures[name] = texture;
            tileMaterials[name] = CreateMaterial("MoonMahjong tile " + name, texture, TileSmoothness);
            handTileMaterials[name] = CreateMaterial("MoonMahjong hand tile " + name, texture, TileSmoothness, HandTint);
            highlightedTileMaterials[name] = CreateMaterial("MoonMahjong highlighted tile " + name, texture, TileSmoothness, HighlightTint);
            dimmedTileMaterials[name] = CreateMaterial("MoonMahjong dimmed tile " + name, texture, TileSmoothness, DimTint);
        }
        HiddenTileMaterial = CreateMaterial("MoonMahjong hidden tile", LoadTexture(Path.Combine(directory, "tiles", "blank.png")), TileSmoothness);
        LoadSounds(directory);

        // The 3D table top (scripts/table_mesh.py), textured from parts of NaoMahjong's table painting.
        TableFrameMesh = LoadMesh(Path.Combine(directory, "table_frame_mesh.json"), "table frame", Vector3.one);
        TableRailMesh = LoadMesh(Path.Combine(directory, "table_rail_mesh.json"), "table rail", Vector3.one);
        TableFeltMesh = LoadMesh(Path.Combine(directory, "table_felt_mesh.json"), "table felt", Vector3.one);
        ConsoleMesh = LoadMesh(Path.Combine(directory, "table_console_mesh.json"), "table console", Vector3.one);
        FrameMaterial = CreateMaterial("MoonMahjong frame", LoadTexture(Path.Combine(directory, "table_frame.png")), WoodSmoothness, FrameTint);
        RailMaterial = CreateMaterial("MoonMahjong rail", LoadTexture(Path.Combine(directory, "table_rail.png")), WoodSmoothness, RailTint);
        FeltMaterial = CreateMaterial("MoonMahjong felt", LoadTexture(Path.Combine(directory, "table_felt.png")), FeltSmoothness, FeltTint);
        ConsoleMaterial = CreateMaterial("MoonMahjong console", LoadTexture(Path.Combine(directory, "table_console.png")), ConsoleSmoothness, ConsoleTint);
    }

    // The painting is darker than it looked in NaoMahjong's brightly lit scene; these bring the lounge's lighting up
    // to the painted colours (the shader multiplies the texture by the base colour, so values above 1 brighten).
    private const float FrameTint = 1.2f;
    private const float RailTint = 1.6f;
    private const float FeltTint = 1.5f;
    private const float ConsoleTint = 1.4f;
    // The local player's own hand is seen face-on, so its gloss reflects little of the bright lounge, and it read
    // duller than the tiles lying on the table, seen at a glancing angle (measured ivory: 153 against 195).
    private const float HandTint = 1.25f;
    // A discard the player may call glows warm (requested); copies of a hovered hand tile are greyed (requested).
    private static readonly Color HighlightTint = new(1.3f, 1.12f, 0.6f, 1f);
    private static readonly Color DimTint = new(0.5f, 0.5f, 0.53f, 1f);

    // URP Lit smoothness: tiles are polished resin, the rail varnished wood, the felt cloth, the console satin.
    private const float TileSmoothness = 0.55f;
    private const float WoodSmoothness = 0.45f;
    private const float FeltSmoothness = 0.05f;
    private const float ConsoleSmoothness = 0.25f;

    // Table meshes span one table-top side (X/Z in [-0.5, 0.5], Y up from the top); the console is one unit wide.
    public Mesh TableFrameMesh { get; }
    public Mesh TableRailMesh { get; }
    public Mesh TableFeltMesh { get; }
    public Mesh ConsoleMesh { get; }
    public Material FrameMaterial { get; }
    public Material RailMaterial { get; }
    public Material ConsoleMaterial { get; }

    public Vector3 TileSize { get; }
    public Mesh TileMesh { get; }

    // OpenRiichi's table sounds (scripts/sound_assets.py).
    public AudioClip Sound(TableSound sound) => sounds[sound];

    private void LoadSounds(string directory)
    {
        var indexPath = Path.Combine(directory, "sounds.json");
        if (!File.Exists(indexPath))
            throw new FileNotFoundException("Moon Mahjong sound index missing", indexPath);
        var index = JsonSerializer.Deserialize<Dictionary<string, SoundData>>(File.ReadAllText(indexPath))
                    ?? throw new InvalidDataException($"{indexPath} is empty");
        foreach (TableSound sound in Enum.GetValues(typeof(TableSound)))
        {
            var name = sound.ToString().ToLowerInvariant();
            if (!index.TryGetValue(name, out var info) || info.channels < 1 || info.rate < 1 || info.frames < 1)
                throw new InvalidDataException($"{indexPath} has no valid entry for '{name}'");
            var path = Path.Combine(directory, "sounds", name + ".f32");
            if (!File.Exists(path))
                throw new FileNotFoundException("Moon Mahjong sound missing", path);
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length != info.frames * info.channels * sizeof(float))
                throw new InvalidDataException($"{path} has {bytes.Length} bytes, not {info.frames} frames of {info.channels} channels");
            var samples = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);
            var channels = info.channels;
            if (sound.IsSpoken()) // heard over a suit radio (requested), in mono
            {
                samples = RadioVoice.Apply(samples, channels, info.rate);
                channels = 1;
            }
            var clip = AudioClip.Create("MoonMahjong " + name, samples.Length / channels, channels, info.rate, false);
            clip.hideFlags = KeepLoaded;
            if (!clip.SetData(new Il2CppStructArray<float>(samples), 0))
                throw new InvalidDataException($"Unity rejected the samples of {path}");
            sounds[sound] = clip;
        }
        Plugin.Logger.LogInfo($"Loaded {sounds.Count} mahjong sounds");
    }
    public Material FeltMaterial { get; }

    // A tile this player hasn't seen (face down, or in someone else's hand): a blank face (tiles/blank.png, the white
    // dragon's face without its index, so it doesn't read as a white dragon) with the usual back and sides.
    public Material HiddenTileMaterial { get; }

    // Riichi sticks are plain white (the layout supplies their size and pose).
    public Material RiichiStickMaterial => riichiStickMaterial ??= CreatePlainMaterial("MoonMahjong riichi stick", new Color(0.95f, 0.95f, 0.93f));

    private Material? riichiStickMaterial;

    public static MahjongAssets Load(string directory, Material template, float scale)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Moon Mahjong assets folder not found: {directory}");
        return new MahjongAssets(directory, template, scale);
    }

    public Material MaterialFor(TileType type, bool redFive, TileLook look = TileLook.Table)
    {
        if (type == TileType.Blank)
            return HiddenTileMaterial;
        var materials = look switch
        {
            TileLook.OwnHand => handTileMaterials,
            TileLook.Highlighted => highlightedTileMaterials,
            TileLook.Dimmed => dimmedTileMaterials,
            _ => tileMaterials,
        };
        return materials[TextureName(type, redFive)];
    }

    // NaoMahjong names: 1-9 + m/p/s, 0 = red five; honors 1z-4z = East..North, 5z-7z = white, green, red dragon.
    public static string TextureName(TileType type, bool redFive)
    {
        var t = (int)type;
        if (t >= (int)TileType.Man1 && t <= (int)TileType.Man9)
            return Suit(t - (int)TileType.Man1 + 1, 'm', redFive);
        if (t >= (int)TileType.Pin1 && t <= (int)TileType.Pin9)
            return Suit(t - (int)TileType.Pin1 + 1, 'p', redFive);
        if (t >= (int)TileType.Sou1 && t <= (int)TileType.Sou9)
            return Suit(t - (int)TileType.Sou1 + 1, 's', redFive);
        if (t >= (int)TileType.Ton && t <= (int)TileType.Chun)
            return (t - (int)TileType.Ton + 1) + "z";
        throw new ArgumentOutOfRangeException(nameof(type), type, "Blank has no texture");

        static string Suit(int number, char suit, bool red) => (red && number == 5 ? 0 : number) + suit.ToString();
    }

    private static IEnumerable<string> AllTileNames()
    {
        foreach (var suit in "mps")
            for (var n = 0; n <= 9; n++)
                yield return n.ToString() + suit;
        for (var n = 1; n <= 7; n++)
            yield return n + "z";
    }

    private Material CreatePlainMaterial(string name, Color color)
    {
        var material = new Material(template) { name = name, hideFlags = KeepLoaded };
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", null);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", null);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.6f);
        return material;
    }

    private Material CreateMaterial(string name, Texture2D texture, float smoothness, float tint = 1f) =>
        CreateMaterial(name, texture, smoothness, new Color(tint, tint, tint, 1f));

    // The shader multiplies the texture by `color` (components above 1 brighten).
    private Material CreateMaterial(string name, Texture2D texture, float smoothness, Color color)
    {
        var material = new Material(template) { name = name, hideFlags = KeepLoaded };
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        return material;
    }

    private static Texture2D LoadTexture(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Moon Mahjong texture missing", path);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = Path.GetFileNameWithoutExtension(path), hideFlags = KeepLoaded };
        if (!ImageConversion.LoadImage(texture, new Il2CppStructArray<byte>(File.ReadAllBytes(path))))
            throw new InvalidDataException($"Unity could not decode {path}");
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Trilinear;
        texture.anisoLevel = 4;
        return texture;
    }

    // A JSON mesh from scripts/fbx_mesh.py or scripts/table_mesh.py, with `size` baked into its positions
    // (tile_mesh.json is a unit box with the face at +Z; the table meshes keep their own units).
    private static Mesh LoadMesh(string path, string meshName, Vector3 size)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Moon Mahjong {meshName} mesh missing", path);
        var data = JsonSerializer.Deserialize<TileMeshData>(File.ReadAllText(path))
                   ?? throw new InvalidDataException($"{path} is empty");
        if (data.positions.Length == 0 || data.positions.Length != data.normals.Length || data.positions.Length != data.uvs.Length
            || data.triangles.Length % 3 != 0)
            throw new InvalidDataException($"{path} has inconsistent mesh arrays");

        var vertices = new Vector3[data.positions.Length];
        var normals = new Vector3[data.positions.Length];
        var uvs = new Vector2[data.positions.Length];
        for (var i = 0; i < vertices.Length; i++)
        {
            var p = data.positions[i];
            var n = data.normals[i];
            vertices[i] = new Vector3(p[0] * size.x, p[1] * size.y, p[2] * size.z);
            // Normals are not re-transformed: every mesh here is scaled uniformly except the tile, whose rounded
            // normals scripts/tile_shape.py computes in the tile's real proportions, which `size` restores.
            normals[i] = new Vector3(n[0], n[1], n[2]).normalized;
            uvs[i] = new Vector2(data.uvs[i][0], data.uvs[i][1]);
        }

        var mesh = new Mesh { name = "MoonMahjong " + meshName, hideFlags = KeepLoaded };
        mesh.vertices = new Il2CppStructArray<Vector3>(vertices);
        mesh.normals = new Il2CppStructArray<Vector3>(normals);
        mesh.uv = new Il2CppStructArray<Vector2>(uvs);
        mesh.triangles = new Il2CppStructArray<int>(data.triangles);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Field names match the JSON written by scripts/sound_assets.py.
    private sealed class SoundData
    {
        public int channels { get; set; }
        public int rate { get; set; }
        public int frames { get; set; }
    }

    // Field names match the JSON written by scripts/fbx_mesh.py.
    private sealed class TileMeshData
    {
        public float[][] positions { get; set; } = Array.Empty<float[]>();
        public float[][] normals { get; set; } = Array.Empty<float[]>();
        public float[][] uvs { get; set; } = Array.Empty<float[]>();
        public int[] triangles { get; set; } = Array.Empty<int>();
    }
}

// How a tile is drawn: lying on the table, in the local player's hand (brighter, see HandTint), highlighted (a discard
// the player may call) or dimmed (a copy of the hovered hand tile).
internal enum TileLook
{
    Table,
    OwnHand,
    Highlighted,
    Dimmed,
}
