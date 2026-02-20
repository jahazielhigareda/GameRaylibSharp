namespace Shared;

public enum Direction : byte
{
    North     = 0,
    NorthEast = 1,
    East      = 2,
    SouthEast = 3,
    South     = 4,
    SouthWest = 5,
    West      = 6,
    NorthWest = 7,
    None      = 255
}

public static class DirectionHelper
{
    /// <summary>
    /// Devuelve el offset (dx, dy) para una dirección.
    /// Norte = Y negativo (arriba en pantalla).
    /// </summary>
    public static (int dx, int dy) ToOffset(Direction dir)
    {
        return dir switch
        {
            Direction.North     => ( 0, -1),
            Direction.NorthEast => ( 1, -1),
            Direction.East      => ( 1,  0),
            Direction.SouthEast => ( 1,  1),
            Direction.South     => ( 0,  1),
            Direction.SouthWest => (-1,  1),
            Direction.West      => (-1,  0),
            Direction.NorthWest => (-1, -1),
            _                   => ( 0,  0)
        };
    }

    public static bool IsDiagonal(Direction dir)
    {
        return dir is Direction.NorthEast or Direction.SouthEast
                   or Direction.SouthWest or Direction.NorthWest;
    }

    public static Direction FromOffset(int dx, int dy) => (dx, dy) switch
    {
        ( 0, -1) => Direction.North,
        ( 1, -1) => Direction.NorthEast,
        ( 1,  0) => Direction.East,
        ( 1,  1) => Direction.SouthEast,
        ( 0,  1) => Direction.South,
        (-1,  1) => Direction.SouthWest,
        (-1,  0) => Direction.West,
        (-1, -1) => Direction.NorthWest,
        _        => Direction.South,
    };

}
