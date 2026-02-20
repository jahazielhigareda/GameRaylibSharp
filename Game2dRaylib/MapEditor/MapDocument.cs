using MapEditor.Maps;

namespace MapEditor;

public sealed class MapDocument
{
    public ushort      Width       { get; private set; }
    public ushort      Height      { get; private set; }
    public byte        Floors      { get; private set; }
    public byte        GroundFloor { get; private set; }
    public MapTile[,,] Tiles       { get; private set; }
    public string?     FilePath    { get; set; }
    public bool        IsDirty     { get; private set; }

    public MapDocument(ushort w, ushort h, byte floors = 8, byte groundFloor = 7)
    {
        Width = w; Height = h; Floors = floors; GroundFloor = groundFloor;
        Tiles = new MapTile[w, h, floors];
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        for (int z = 0; z < floors; z++)
            Tiles[x, y, z] = new MapTile
            {
                GroundItemId = (ushort)TileType.Grass,
                Flags        = TileFlags.Walkable,
                Items        = Array.Empty<ItemInstance>(),
            };
    }

    public void MarkDirty() => IsDirty = true;

    public MapData ToMapData() => new(Width, Height, Floors, GroundFloor, Tiles);

    public static MapDocument FromMapData(MapData md, string? path = null)
    {
        var doc = new MapDocument(md.Width, md.Height, md.Floors, md.GroundFloor);
        for (int x = 0; x < md.Width;  x++)
        for (int y = 0; y < md.Height; y++)
        for (int z = 0; z < md.Floors; z++)
            doc.Tiles[x, y, z] = md.Tiles[x, y, z];
        doc.FilePath = path;
        doc.IsDirty  = false;
        return doc;
    }

    public void Save(string path)
    {
        MapSerializer.Write(ToMapData(), path);
        FilePath = path;
        IsDirty  = false;
    }

    public static MapDocument Load(string path)
        => FromMapData(MapSerializer.Read(path), path);

    public (ushort[,] ids, ushort[,] flags) CopyRegion(int x, int y, int w, int h, int floor)
    {
        var ids   = new ushort[w, h];
        var flags = new ushort[w, h];
        for (int dx = 0; dx < w; dx++)
        for (int dy = 0; dy < h; dy++)
        {
            int tx = x+dx, ty = y+dy;
            if (tx < 0||tx>=Width||ty<0||ty>=Height) continue;
            ids[dx,dy]   = Tiles[tx,ty,floor].GroundItemId;
            flags[dx,dy] = (ushort)Tiles[tx,ty,floor].Flags;
        }
        return (ids, flags);
    }
}
