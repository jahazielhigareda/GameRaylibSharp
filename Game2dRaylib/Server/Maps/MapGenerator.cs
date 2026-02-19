namespace Server.Maps;

/// <summary>
/// Procedurally generates a sample 32×32×8 map.
/// Tibia convention: groundFloor = 7 (surface visible al jugador).
/// Floors 0..6 son subterráneos (stone no-walkable).
/// Floor 7 = superficie con Grass, Water, Trees.
/// </summary>
public static class MapGenerator
{
    private const ushort GrassId = 1231;
    private const ushort StoneId = 1055;
    private const ushort WaterId = 4608;
    private const ushort TreeId  = 2700;

    public static MapData Generate(ushort width = 32, ushort height = 32, byte floors = 8)
    {
        const byte groundFloor = 7;   // Tibia: 7 = superficie
        var tiles = new MapTile[width, height, floors];

        for (int x = 0; x < width;  x++)
        for (int y = 0; y < height; y++)
        for (int z = 0; z < floors; z++)
        {
            // Floors subterráneos (0-6): stone sólido, no walkable
            if (z != groundFloor)
            {
                tiles[x, y, z] = Stone();
                continue;
            }

            // Floor 7 = superficie - layout del mundo
            bool isBorder = x == 0 || x == width  - 1 ||
                            y == 0 || y == height - 1;
            bool isWater  = !isBorder &&
                            (x == 4 || y == 4 || x == width - 5 || y == height - 5);
            bool isTree   = !isBorder && !isWater && IsForestPatch(x, y);

            if      (isBorder) tiles[x, y, z] = Stone();
            else if (isWater)  tiles[x, y, z] = Water();
            else if (isTree)   tiles[x, y, z] = Tree();
            else               tiles[x, y, z] = Grass();
        }

        return new MapData(width, height, floors, groundFloor, tiles);
    }

    private static bool IsForestPatch(int x, int y)
    {
        (int, int)[] origins = { (8, 8), (20, 10), (14, 20), (24, 24) };
        foreach (var (ox, oy) in origins)
            if (x >= ox && x < ox + 4 && y >= oy && y < oy + 4)
                return true;
        return false;
    }

    private static MapTile Stone() =>
        new() { GroundItemId = StoneId, Flags = TileFlags.None,            Items = Array.Empty<ItemInstance>() };
    private static MapTile Grass() =>
        new() { GroundItemId = GrassId, Flags = TileFlags.Walkable,        Items = Array.Empty<ItemInstance>() };
    private static MapTile Water() =>
        new() { GroundItemId = WaterId, Flags = TileFlags.None,            Items = Array.Empty<ItemInstance>() };
    private static MapTile Tree()  =>
        new() { GroundItemId = TreeId,  Flags = TileFlags.BlockProjectile, Items = Array.Empty<ItemInstance>() };
}
