using K4os.Compression.LZ4.Streams;

namespace Server.Maps;

/// <summary>Item instance stacked on a tile.</summary>
public struct ItemInstance
{
    public ushort ItemId;
    public byte   Count;
    public ushort ActionId;
    public ushort UniqueId;
}

/// <summary>One cell in the map grid.</summary>
public struct MapTile
{
    public ushort       GroundItemId;
    public TileFlags    Flags;
    public ItemInstance[] Items;

    public TileType GroundType  => TileTypeHelper.FromGroundId(GroundItemId);
    public bool     IsWalkable  => (Flags & TileFlags.Walkable) != 0;
}

/// <summary>
/// Fully-loaded map. Tiles are indexed [x, y, floor].
/// WalkableMap is precomputed for fast IsWalkable lookups.
/// </summary>
public sealed class MapData
{
    public ushort      Width       { get; }
    public ushort      Height      { get; }
    public byte        Floors      { get; }
    public byte        GroundFloor { get; }   // canonical ground-level index (7)
    public MapTile[,,] Tiles       { get; }
    public bool[,,]    Walkable    { get; }   // Walkable[x,y,z]

    public MapData(ushort width, ushort height, byte floors, byte groundFloor,
                   MapTile[,,] tiles)
    {
        Width       = width;
        Height      = height;
        Floors      = floors;
        GroundFloor = groundFloor;
        Tiles       = tiles;

        Walkable = new bool[width, height, floors];
        for (int x = 0; x < width;  x++)
        for (int y = 0; y < height; y++)
        for (int z = 0; z < floors; z++)
            Walkable[x, y, z] = tiles[x, y, z].IsWalkable;
    }

    /// <summary>2-D ground-floor walkability helper for MovementSystem.</summary>
    public bool IsWalkable2D(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return false;
        return Walkable[x, y, 0];
    }
}

/// <summary>
/// Wire format helpers.
///
/// File layout:
///   [4]  "GMAP" magic
///   [2]  version (ushort LE) = 1
///   [4]  original (uncompressed) tile-data size (int LE)
///   [*]  LZ4-block-compressed tile data
///
/// Tile data (uncompressed):
///   [2] width  ushort
///   [2] height ushort
///   [1] floors byte
///   [1] groundFloor byte
///   for x in [0,width):
///     for y in [0,height):
///       for z in [0,floors):
///         [2] groundItemId
///         [2] flags
///         [1] itemCount
///         for i in [0,itemCount):
///           [2] itemId
///           [1] count
///           [2] actionId
///           [2] uniqueId
/// </summary>
public static class MapSerializer
{
    private static readonly byte[] Magic   = { (byte)'G',(byte)'M',(byte)'A',(byte)'P' };
    public  const           ushort Version = 1;

    // ── Serialize ─────────────────────────────────────────────────────────

    public static void Write(MapData map, string path)
    {
        var raw = EncodeTileData(map);

        int originalSize = raw.Length;

        var compMs = new MemoryStream();
        using (var lz4 = LZ4Stream.Encode(compMs, leaveOpen: true))
            lz4.Write(raw, 0, raw.Length);
        var compressed = compMs.ToArray();

        using var fs  = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw  = new BinaryWriter(fs);

        bw.Write(Magic);
        bw.Write(Version);
        bw.Write(originalSize);
        bw.Write(compressed);
    }

    private static byte[] EncodeTileData(MapData map)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write(map.Width);
        bw.Write(map.Height);
        bw.Write(map.Floors);
        bw.Write(map.GroundFloor);

        for (int x = 0; x < map.Width;  x++)
        for (int y = 0; y < map.Height; y++)
        for (int z = 0; z < map.Floors; z++)
        {
            var t = map.Tiles[x, y, z];
            bw.Write(t.GroundItemId);
            bw.Write((ushort)t.Flags);
            bw.Write((byte)(t.Items?.Length ?? 0));
            if (t.Items != null)
                foreach (var item in t.Items)
                {
                    bw.Write(item.ItemId);
                    bw.Write(item.Count);
                    bw.Write(item.ActionId);
                    bw.Write(item.UniqueId);
                }
        }

        return ms.ToArray();
    }

    // ── Deserialize ───────────────────────────────────────────────────────

    public static MapData Read(string path)
    {
        var raw = File.ReadAllBytes(path);

        // magic
        if (raw.Length < 10 ||
            raw[0] != Magic[0] || raw[1] != Magic[1] ||
            raw[2] != Magic[2] || raw[3] != Magic[3])
            throw new InvalidDataException("Not a GMAP file.");

        ushort version = (ushort)(raw[4] | (raw[5] << 8));
        if (version != Version)
            throw new InvalidDataException($"Unsupported GMAP version {version}.");

        int originalSize = raw[6] | (raw[7] << 8) | (raw[8] << 16) | (raw[9] << 24);

        var compressedBytes = raw.AsSpan(10).ToArray();
        var decompressed    = new byte[originalSize];
        using (var compMs = new MemoryStream(compressedBytes))
        using (var lz4    = LZ4Stream.Decode(compMs))
        {
            int total = 0;
            while (total < originalSize)
            {
                int n = lz4.Read(decompressed, total, originalSize - total);
                if (n == 0) break;
                total += n;
            }
            if (total != originalSize)
                throw new InvalidDataException(
                    $"LZ4 decode error: expected {originalSize} bytes, got {total}.");
        }

        return DecodeTileData(decompressed);
    }

    private static MapData DecodeTileData(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        ushort width       = br.ReadUInt16();
        ushort height      = br.ReadUInt16();
        byte   floors      = br.ReadByte();
        byte   groundFloor = br.ReadByte();

        var tiles = new MapTile[width, height, floors];

        for (int x = 0; x < width;  x++)
        for (int y = 0; y < height; y++)
        for (int z = 0; z < floors; z++)
        {
            ushort groundId  = br.ReadUInt16();
            ushort flagsRaw  = br.ReadUInt16();
            byte   itemCount = br.ReadByte();

            var items = new ItemInstance[itemCount];
            for (int i = 0; i < itemCount; i++)
                items[i] = new ItemInstance
                {
                    ItemId   = br.ReadUInt16(),
                    Count    = br.ReadByte(),
                    ActionId = br.ReadUInt16(),
                    UniqueId = br.ReadUInt16(),
                };

            tiles[x, y, z] = new MapTile
            {
                GroundItemId = groundId,
                Flags        = (TileFlags)flagsRaw,
                Items        = items,
            };
        }

        return new MapData(width, height, floors, groundFloor, tiles);
    }
}
