using Server.Maps;

namespace Server.Maps;

/// <summary>
/// Line-of-Sight check using Bresenham's line algorithm.
///
/// Canary equivalent: game/game.cpp hasLineOfSight() / canThrowObjectTo()
///
/// A ray is traced from (x0,y0) to (x1,y1) on the same floor.
/// The check fails if any intermediate tile has the BlockProjectile flag set.
/// The start and end tiles are NOT checked (attacker/target tile is never blocked).
/// </summary>
public static class LosChecker
{
    /// <summary>
    /// Returns true if there is unobstructed line-of-sight between
    /// two points on the same floor of the given map.
    /// Always returns true if the two points are the same or adjacent.
    /// </summary>
    public static bool HasLineOfSight(
        MapData map,
        int x0, int y0,
        int x1, int y1,
        byte floor)
    {
        // Same tile or adjacent – always visible
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        if (dx <= 1 && dy <= 1) return true;

        int sx = x1 > x0 ? 1 : -1;
        int sy = y1 > y0 ? 1 : -1;

        int err = dx - dy;

        int cx = x0;
        int cy = y0;

        while (true)
        {
            // Reached destination
            if (cx == x1 && cy == y1) return true;

            // Check if this intermediate tile blocks projectiles
            // (we skip the very first tile = attacker position)
            if ((cx != x0 || cy != y0) && IsBlocking(map, cx, cy, floor))
                return false;

            int e2 = 2 * err;

            if (e2 > -dy)
            {
                err -= dy;
                cx  += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                cy  += sy;
            }
        }
    }

    private static bool IsBlocking(MapData map, int x, int y, byte floor)
    {
        if (x < 0 || x >= map.Width || y < 0 || y >= map.Height) return true;
        var flags = map.Tiles[x, y, floor].Flags;
        // A tile blocks LoS if it is not walkable OR has the BlockProjectile flag
        return (flags & TileFlags.BlockProjectile) != 0
            || (flags & TileFlags.Walkable) == 0;
    }
}
