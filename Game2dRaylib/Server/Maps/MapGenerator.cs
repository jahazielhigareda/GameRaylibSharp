namespace Server.Maps;

/// <summary>
/// Procedurally generates a sample 32×32×8 map for development.
/// Layout (ground floor z=0):
///   Border ring    → Stone,    non-walkable
///   Water band     → Water,    non-walkable  (x==4 or y==4 inset ring)
///   Forest patches → Tree,     non-walkable  (scattered 4×4 blobs)
///   Interior       → Grass,    walkable
/// Floors z=1..7 are filled with stone (non-walkable) for now.
/// </summary>
public static class MapGenerator
{
    private const ushort GrassId = 1231;
    private const ushort StoneId = 1055;
    private const ushort WaterId = 4608;
    private const ushort TreeId  = 2700;

    public static MapData Generate(ushort width = 32, ushort height = 32, byte floors = 8)
    {
        const byte groundFloor = 7;   // Tibia convention stored in header
        var tiles = new MapTile[width, height, floors];

        for (int x = 0; x < width;  x++)
        for (int y = 0; y < height; y++)
        for (int z = 0; z < floors; z++)
        {
            if (z != 0)
            {
                tiles[x, y, z] = Stone();
                continue;
            }

            // Ground floor layout
            bool isBorder = x == 0 || x == width  - 1 ||
                            y == 0 || y == height - 1;
            bool isWater  = !isBorder &&
                            (x == 4 || y == 4 || x == width - 5 || y == height - 5);
            bool isTree   = !isBorder && !isWater && IsForestPatch(x, y);

            if (isBorder)       tiles[x, y, z] = Stone();
            else if (isWater)   tiles[x, y, z] = Water();
            else if (isTree)    tiles[x, y, z] = Tree();
            else                tiles[x, y, z] = Grass();
        }

        return new MapData(width, height, floors, groundFloor, tiles);
    }

    // Forest blobs at fixed positions
    private static bool IsForestPatch(int x, int y)
    {
        (int, int)[] origins = { (8, 8), (20, 10), (14, 20), (24, 24) };
        foreach (var (ox, oy) in origins)
            if (x >= ox && x < ox + 4 && y >= oy && y < oy + 4)
                return true;
        return false;
    }

    private static MapTile Stone() =>
        new() { GroundItemId = StoneId, Flags = TileFlags.None,     Items = Array.Empty<ItemInstance>() };
    private static MapTile Grass() =>
        new() { GroundItemId = GrassId, Flags = TileFlags.Walkable, Items = Array.Empty<ItemInstance>() };
    private static MapTile Water() =>
        new() { GroundItemId = WaterId, Flags = TileFlags.None,     Items = Array.Empty<ItemInstance>() };
    private static MapTile Tree()  =>
        new() { GroundItemId = TreeId,  Flags = TileFlags.BlockProjectile, Items = Array.Empty<ItemInstance>() };
}
