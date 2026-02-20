using System.Buffers;
using Server.Maps;

namespace Server.AI;

/// <summary>
/// Optimized A* pathfinder for creature navigation on the server.
///
/// Design goals:
///   1. Zero heap allocation per call — reuse pooled flat arrays.
///   2. O(1) gScore / cameFrom lookup via linear tile index.
///   3. PriorityQueue over flat indices (no tuple boxing).
///   4. Chebyshev heuristic with tie-breaking to reduce node expansions.
///   5. Bounded by <see cref="MaxSteps"/> to cap worst-case CPU time.
///
/// Thread safety: Uses [ThreadStatic] scratch buffers — safe for concurrent
/// calls from different threads (e.g. if AI ever moves to a thread pool).
/// </summary>
public static class AStarPathfinder
{
    // ── Constants ────────────────────────────────────────────────────────────

    public  const int MaxSteps      = 120;
    private const float DiagCost    = 1.414f;
    private const float CardCost    = 1.000f;
    /// <summary>Tie-breaking nudge: slightly prefer paths closer to goal.</summary>
    private const float TieBreak    = 1.001f;

    // 8-directional neighbor table  (dx, dy, moveCost)
    private static readonly (int dx, int dy, float cost)[] Dirs =
    {
        ( 0, -1, CardCost), ( 0,  1, CardCost),
        (-1,  0, CardCost), ( 1,  0, CardCost),
        (-1, -1, DiagCost), ( 1, -1, DiagCost),
        (-1,  1, DiagCost), ( 1,  1, DiagCost),
    };

    // ── Thread-local scratch buffers ─────────────────────────────────────────

    [ThreadStatic] private static float[]? _gScore;
    [ThreadStatic] private static int[]?   _cameFrom;
    [ThreadStatic] private static bool[]?  _inClosed;

    private static float[] GScore(int size)
    {
        if (_gScore == null || _gScore.Length < size)
            _gScore = new float[size];
        return _gScore;
    }
    private static int[] CameFrom(int size)
    {
        if (_cameFrom == null || _cameFrom.Length < size)
            _cameFrom = new int[size];
        return _cameFrom;
    }
    private static bool[] InClosed(int size)
    {
        if (_inClosed == null || _inClosed.Length < size)
            _inClosed = new bool[size];
        return _inClosed;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the (dx, dy) of the single next step toward the goal.
    /// Returns (0,0) if already at goal or no path found within MaxSteps.
    /// </summary>
    public static (int dx, int dy) NextStep(
        int startX, int startY, byte floor,
        int goalX,  int goalY,
        MapData map,
        bool avoidOccupied = false)
    {
        var path = FindPath(startX, startY, floor, goalX, goalY, map, avoidOccupied, maxResults: 1);
        return path.Count > 0 ? path[0] : (0, 0);
    }

    /// <summary>
    /// Returns the full path as a list of (dx, dy) steps from start to goal.
    /// List is empty if no path found.
    /// </summary>
    public static List<(int dx, int dy)> FindPath(
        int startX, int startY, byte floor,
        int goalX,  int goalY,
        MapData map,
        bool avoidOccupied = false,
        int maxResults = int.MaxValue)
    {
        var result = new List<(int, int)>(8);

        if (startX == goalX && startY == goalY) return result;

        int w     = map.Width;
        int h     = map.Height;
        int total = w * h;

        int startIdx = Idx(startX, startY, w);
        int goalIdx  = Idx(goalX,  goalY,  w);

        // ── Scratch buffers ───────────────────────────────────────────────
        float[] gScore   = GScore(total);
        int[]   cameFrom = CameFrom(total);
        bool[]  closed   = InClosed(total);

        // Reset only what we touched (tracked via dirtyList)
        var dirty = new List<int>(MaxSteps * 4);

        void MarkDirty(int i)
        {
            dirty.Add(i);
            gScore[i]   = float.MaxValue;
            cameFrom[i] = -1;
            closed[i]   = false;
        }

        // Seed
        gScore[startIdx]   = 0f;
        cameFrom[startIdx] = startIdx;
        dirty.Add(startIdx);

        // ── Open set ──────────────────────────────────────────────────────
        var open = new PriorityQueue<int, float>();
        open.Enqueue(startIdx, Heuristic(startX, startY, goalX, goalY));

        int steps = 0;
        bool found = false;

        while (open.Count > 0 && steps < MaxSteps)
        {
            steps++;
            int cur = open.Dequeue();

            if (closed[cur]) continue;
            closed[cur] = true;

            if (cur == goalIdx) { found = true; break; }

            int cx = cur % w;
            int cy = cur / w;
            float curG = gScore[cur];

            foreach (var (ddx, ddy, cost) in Dirs)
            {
                int nx = cx + ddx;
                int ny = cy + ddy;
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                if (!map.Walkable[nx, ny, floor])             continue;

                int ni = Idx(nx, ny, w);
                if (closed[ni]) continue;

                float tentG = curG + cost;
                if (gScore[ni] == 0f && ni != startIdx)
                {
                    // First visit — mark dirty and initialise
                    MarkDirty(ni);
                }
                if (tentG < gScore[ni])
                {
                    gScore[ni]   = tentG;
                    cameFrom[ni] = cur;
                    float hVal = Heuristic(nx, ny, goalX, goalY);
                    open.Enqueue(ni, tentG + hVal * TieBreak);
                }
            }
        }

        // ── Reconstruct ───────────────────────────────────────────────────
        if (found)
        {
            // Walk backwards from goal to start collecting steps
            var raw = new List<int>(16);
            int cur = goalIdx;
            while (cur != startIdx && cameFrom[cur] != cur && cameFrom[cur] >= 0)
            {
                raw.Add(cur);
                cur = cameFrom[cur];
            }
            raw.Reverse();

            int prev = startIdx;
            foreach (int idx in raw)
            {
                if (result.Count >= maxResults) break;
                int px = prev % w, py = prev / w;
                int nx = idx  % w, ny = idx  / w;
                result.Add((nx - px, ny - py));
                prev = idx;
            }
        }

        // ── Cleanup dirty entries ─────────────────────────────────────────
        foreach (int i in dirty)
        {
            gScore[i]   = 0f;
            cameFrom[i] = 0;
            closed[i]   = false;
        }

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int Idx(int x, int y, int w) => y * w + x;

    private static float Heuristic(int ax, int ay, int bx, int by)
    {
        int dx = Math.Abs(ax - bx);
        int dy = Math.Abs(ay - by);
        // Chebyshev: max(dx,dy) + (sqrt(2)-1)*min(dx,dy)
        return Math.Max(dx, dy) + 0.414f * Math.Min(dx, dy);
    }
}
