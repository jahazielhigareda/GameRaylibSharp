namespace Server.AI;

/// <summary>
/// Per-creature path cache.
/// Stores the remaining steps of the last A* result so the pathfinder
/// is not called every tick — only when the goal changes or the cache expires.
///
/// Usage: call <see cref="TryConsume"/> each tick.
/// If it returns false, compute a new path and call <see cref="Store"/>.
/// </summary>
public sealed class PathCache
{
    /// <summary>Max ticks a cached path is considered valid.</summary>
    private const int TtlTicks = 10;

    private readonly record struct Entry(
        int GoalX, int GoalY,
        List<(int dx, int dy)> Steps,
        int StepIndex,
        int ExpiryTick);

    private readonly Dictionary<int, Entry> _cache = new();
    private int _tick;

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Advance the internal tick counter (call once per server tick).</summary>
    public void Tick() => _tick++;

    /// <summary>
    /// Try to consume the next cached step for <paramref name="entityId"/>.
    /// Returns true and sets <paramref name="dx"/>,<paramref name="dy"/> if
    /// a valid cached step exists for the same goal.
    /// </summary>
    public bool TryConsume(int entityId, int goalX, int goalY,
                           out int dx, out int dy)
    {
        dx = 0; dy = 0;

        if (!_cache.TryGetValue(entityId, out var entry))
            return false;

        // Invalidate if goal changed or TTL expired
        if (entry.GoalX != goalX || entry.GoalY != goalY || _tick > entry.ExpiryTick)
        {
            _cache.Remove(entityId);
            return false;
        }

        // Exhausted all steps
        if (entry.StepIndex >= entry.Steps.Count)
        {
            _cache.Remove(entityId);
            return false;
        }

        var step = entry.Steps[entry.StepIndex];
        dx = step.dx;
        dy = step.dy;

        // Advance index
        _cache[entityId] = entry with { StepIndex = entry.StepIndex + 1 };
        return true;
    }

    /// <summary>
    /// Store a freshly-computed path for <paramref name="entityId"/>.
    /// Immediately consumes the first step and returns it.
    /// </summary>
    public (int dx, int dy) Store(int entityId, int goalX, int goalY,
                                  List<(int dx, int dy)> steps)
    {
        if (steps.Count == 0)
        {
            _cache.Remove(entityId);
            return (0, 0);
        }

        _cache[entityId] = new Entry(goalX, goalY, steps, 1, _tick + TtlTicks);
        return steps[0];
    }

    /// <summary>Remove all cached entries for an entity (e.g. on death or state change).</summary>
    public void Invalidate(int entityId) => _cache.Remove(entityId);

    /// <summary>Clear entire cache (e.g. on map reload).</summary>
    public void Clear() => _cache.Clear();
}
