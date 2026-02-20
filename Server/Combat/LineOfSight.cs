using Server.Maps;

namespace Server.Combat;

/// <summary>
/// Bresenham line-based Line of Sight checker.
///
/// A tile blocks LoS when its TileFlags has the BlockProjectile bit set.
/// The source tile is never tested; the destination tile IS tested so that
/// creatures behind walls are not visible.
///
/// Used by:
///   - CombatSystem     : distance attacks require unobstructed LoS.
///   - CreatureAiSystem : creatures only detect players they can "see".
///   - NetworkManager   : target validation on TargetRequestPacket.
/// </summary>
public static class LineOfSight
{
    /// <summary>
    /// Returns true if there is an unobstructed line of sight from
    /// (x0, y0) to (x1, y1) on the given floor.
    /// </summary>
    public static bool HasLoS(int x0, int y0, int x1, int y1, byte floor, MapData map)
    {
        if (x0 == x1 && y0 == y1) return true;

        int dx  = Math.Abs(x1 - x0);
        int dy  = Math.Abs(y1 - y0);
        int sx  = x0 < x1 ? 1 : -1;
        int sy  = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        int cx = x0;
        int cy = y0;

        while (true)
        {
            // Skip source tile; check every other tile including destination
            if (cx != x0 || cy != y0)
            {
                if (!InBounds(cx, cy, map)) return false;
                if (BlocksProjectile(cx, cy, floor, map)) return false;
            }

            if (cx == x1 && cy == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; cx += sx; }
            if (e2 <  dx) { err += dx; cy += sy; }
        }

        return true;
    }

    /// <summary>Convenience overload – uses floor 0.</summary>
    public static bool HasLoS(int x0, int y0, int x1, int y1, MapData map)
        => HasLoS(x0, y0, x1, y1, 0, map);

    private static bool InBounds(int x, int y, MapData map)
        => x >= 0 && y >= 0 && x < map.Width && y < map.Height;

    private static bool BlocksProjectile(int x, int y, byte floor, MapData map)
    {
        if (floor >= map.Floors) return true;
        return (map.Tiles[x, y, floor].Flags & TileFlags.BlockProjectile) != 0;
    }
}
