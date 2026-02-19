using Server.Events;
using Arch.Core;
using Arch.Core.Extensions;
using Server.ECS.Components;

namespace Server.Spatial;

/// <summary>
/// Grid-based spatial hash for AoI (Area of Interest) filtering.
/// CELL_SIZE = 32 tiles.  Each player sees a 3×3 grid of cells around itself.
/// </summary>
public sealed class SpatialHashGrid
{
    private EventBus? _eventBus;

    public void SetEventBus(EventBus bus) => _eventBus = bus;
    private const int CellSize = 32; // tiles per cell

    private readonly Dictionary<(int, int), HashSet<Entity>> _cells = new();

    // ── Mutation ──────────────────────────────────────────────────────────

    public void Clear() => _cells.Clear();

    public void Add(Entity entity, int tileX, int tileY)
    {
        var key = CellKey(tileX, tileY);
        if (!_cells.TryGetValue(key, out var set))
        {
            set = new HashSet<Entity>();
            _cells[key] = set;
        }
        bool added = set.Add(entity);
        if (added && _eventBus != null)
        {
            int eid = entity.Has<Server.ECS.Components.NetworkIdComponent>()
                ? entity.Get<Server.ECS.Components.NetworkIdComponent>().Id
                : entity.Id;
            _eventBus.Publish(new EntityEnteredCell
            {
                EntityId = eid,
                CellX    = key.Item1,
                CellY    = key.Item2,
                TileX    = tileX,
                TileY    = tileY
            });
        }
    }

    // ── Query ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all entities visible from (tileX, tileY):
    /// the 3×3 neighbourhood of cells centred on the observer's cell.
    /// </summary>
    public IEnumerable<Entity> GetVisible(int tileX, int tileY)
    {
        int cx = CellX(tileX);
        int cy = CellY(tileY);

        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            var key = (cx + dx, cy + dy);
            if (_cells.TryGetValue(key, out var set))
                foreach (var e in set)
                    yield return e;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static int CellX(int tileX) => tileX / CellSize;
    private static int CellY(int tileY) => tileY / CellSize;
    private static (int, int) CellKey(int tileX, int tileY)
        => (CellX(tileX), CellY(tileY));
}
