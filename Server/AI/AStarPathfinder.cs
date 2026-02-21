using Server.Maps;

namespace Server.AI;

/// <summary>
/// A* pathfinder for creature navigation — Tibia-accurate design.
///
/// KEY DESIGN (matching TFS/Canary behavior):
///   • A* considers ONLY map tile walkability (walls, water, etc.).
///     It IGNORES creature and player occupancy. This means A* always
///     finds the geometrically optimal path even if some tiles are
///     temporarily blocked by other creatures.
///   • Actual movement is gated by MovementSystem.IsWalkable3D which
///     DOES check occupancy. If blocked, the creature retries next tick.
///   • The pathfind goal is the NEAREST tile adjacent to the target
///     (Chebyshev dist == 1), never the target's own tile. This prevents
///     "no path" failures when the target tile is occupied.
///   • Chebyshev heuristic supports 8-directional movement.
///   • Bounded by MaxSteps to cap worst-case CPU per tick.
///   • ThreadStatic scratch buffers — zero heap alloc per call.
/// </summary>
public static class AStarPathfinder
{
    public const int MaxSteps      = 200;
    private const float DiagCost   = 1.414f;
    private const float CardCost   = 1.000f;
    private const float TieBreak   = 1.001f;

    // 8 directions: cardinal first (preferred for Tibia-style movement)
    private static readonly (int dx, int dy, float cost)[] Dirs =
    {
        ( 0, -1, CardCost), ( 0,  1, CardCost),
        (-1,  0, CardCost), ( 1,  0, CardCost),
        (-1, -1, DiagCost), ( 1, -1, DiagCost),
        (-1,  1, DiagCost), ( 1,  1, DiagCost),
    };

    [ThreadStatic] private static float[]? _gScore;
    [ThreadStatic] private static int[]?   _cameFrom;
    [ThreadStatic] private static bool[]?  _inClosed;

    private static float[] GScore(int n)   { if (_gScore   == null || _gScore.Length   < n) _gScore   = new float[n]; return _gScore;   }
    private static int[]   CameFrom(int n) { if (_cameFrom == null || _cameFrom.Length < n) _cameFrom = new int[n];   return _cameFrom; }
    private static bool[]  InClosed(int n) { if (_inClosed == null || _inClosed.Length < n) _inClosed = new bool[n];  return _inClosed; }

    private static int   Idx(int x, int y, int w) => y * w + x;
    private static int   Chebyshev(int ax, int ay, int bx, int by) => Math.Max(Math.Abs(ax-bx), Math.Abs(ay-by));
    private static float Heuristic(int ax, int ay, int bx, int by)
    {
        int dx = Math.Abs(ax - bx), dy = Math.Abs(ay - by);
        return Math.Max(dx, dy) + 0.414f * Math.Min(dx, dy);
    }

    /// <summary>
    /// Returns the single next (dx, dy) step to move from (startX,startY) toward
    /// (goalX,goalY). Stops when Chebyshev distance to goal is 1 (adjacent).
    /// Returns (0,0) if already adjacent or no path found.
    /// Only considers map-tile walkability — ignores entity occupancy.
    /// </summary>
    public static (int dx, int dy) NextStep(
        int startX, int startY, byte floor,
        int goalX,  int goalY,
        MapData map)
    {
        // Already adjacent — nothing to do
        if (Chebyshev(startX, startY, goalX, goalY) <= 1)
            return (0, 0);

        int w = map.Width, h = map.Height, total = w * h;
        int startIdx = Idx(startX, startY, w);

        float[] g  = GScore(total);
        int[]   cf = CameFrom(total);
        bool[]  cl = InClosed(total);

        var dirty = new List<int>(MaxSteps * 5);

        // Initialize start
        g[startIdx]  = 0f;
        cf[startIdx] = startIdx;
        dirty.Add(startIdx);

        var open = new PriorityQueue<int, float>();
        open.Enqueue(startIdx, Heuristic(startX, startY, goalX, goalY));

        int steps   = 0;
        int bestIdx = startIdx;
        int bestDist = Chebyshev(startX, startY, goalX, goalY);

        while (open.Count > 0 && steps < MaxSteps)
        {
            steps++;
            int cur = open.Dequeue();
            if (cl[cur]) continue;
            cl[cur] = true;

            int cx = cur % w, cy = cur / w;
            int d  = Chebyshev(cx, cy, goalX, goalY);

            // Track closest node visited
            if (d < bestDist) { bestDist = d; bestIdx = cur; }

            // SUCCESS: adjacent to goal
            if (d <= 1) break;

            float curG = g[cur];
            foreach (var (ddx, ddy, cost) in Dirs)
            {
                int nx = cx + ddx, ny = cy + ddy;
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;

                // ONLY check map walkability — ignore creature/player occupancy
                if (!map.Walkable[nx, ny, floor]) continue;

                int ni = Idx(nx, ny, w);
                if (cl[ni]) continue;

                // First visit: init
                if (g[ni] == 0f && ni != startIdx)
                {
                    g[ni]  = float.MaxValue;
                    cf[ni] = -1;
                    dirty.Add(ni);
                }

                float tentG = curG + cost;
                if (tentG < g[ni])
                {
                    g[ni]  = tentG;
                    cf[ni] = cur;
                    open.Enqueue(ni, tentG + Heuristic(nx, ny, goalX, goalY) * TieBreak);
                }
            }
        }

        // Trace back from bestIdx to find first step after start
        (int dx, int dy) result = (0, 0);
        if (bestIdx != startIdx)
        {
            int cur = bestIdx;
            while (cf[cur] != startIdx && cf[cur] != cur && cf[cur] >= 0)
                cur = cf[cur];

            if (cf[cur] == startIdx)
            {
                int nx = cur % w, ny = cur / w;
                result = (nx - startX, ny - startY);
            }
        }

        // Cleanup touched entries
        foreach (int i in dirty)
        {
            g[i]  = 0f;
            cf[i] = 0;
            cl[i] = false;
        }

        return result;
    }

    /// <summary>Legacy overload — used by Return-to-spawn (static goal).</summary>
    public static List<(int dx, int dy)> FindPath(
        int startX, int startY, byte floor,
        int goalX, int goalY, MapData map,
        bool avoidOccupied = false, int maxResults = int.MaxValue)
    {
        var result = new List<(int, int)>(8);
        var (dx, dy) = NextStep(startX, startY, floor, goalX, goalY, map);
        if (dx != 0 || dy != 0) result.Add((dx, dy));
        return result;
    }
}
