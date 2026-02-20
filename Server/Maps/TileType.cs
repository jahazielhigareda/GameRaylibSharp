namespace Server.Maps;

public enum TileType : ushort
{
    Unknown = 0,
    Grass   = 1231,
    Stone   = 1055,
    Water   = 4608,
    Sand    = 231,
    Tree    = 2700,
    Wall     = 1,
    StairUp  = 420,
    StairDown= 421,
    RopeSpot = 3866,
}

public static class TileTypeHelper
{
    public static TileType FromGroundId(ushort id) => id switch
    {
        1231 => TileType.Grass,
        1055 => TileType.Stone,
        4608 => TileType.Water,
        231  => TileType.Sand,
        2700 => TileType.Tree,
        1    => TileType.Wall,
        420  => TileType.StairUp,
        421  => TileType.StairDown,
        3866 => TileType.RopeSpot,
        _    => TileType.Unknown,
    };

    public static bool IsWalkableByDefault(TileType t) => t switch
    {
        TileType.Grass => true,
        TileType.Sand  => true,
        _              => false,
    };
}
