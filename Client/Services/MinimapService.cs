using Client.ECS.Systems;
using Raylib_cs;

namespace Client.Services;

/// <summary>
/// Maintains the client-side minimap state: which tiles the player has
/// explored and what colour each tile should be shown as on the minimap.
///
/// Lifecycle:
///   1. <see cref="InitFromGrid3D"/> or <see cref="InitFromGrid"/> is called
///      once when map data arrives (forwarded from TileRenderSystem).
///   2. <see cref="MarkVisible"/> is called every frame from TileRenderSystem
///      with the currently visible tile rectangle; any tile inside is marked
///      as explored forever.
///   3. <see cref="MinimapSystem"/> reads the state via <see cref="TryGetTile"/>
///      to draw the minimap widget.
/// </summary>
public sealed class MinimapService
{
    // Per-tile minimap colour (derived from TileCell.GroundColor)
    private Color[,]? _colors;
    // True once the player has seen a tile
    private bool[,]?  _explored;

    public int  MapWidth  { get; private set; }
    public int  MapHeight { get; private set; }
    public bool HasMap    => _colors != null;

    // ── Initialisation ────────────────────────────────────────────────────

    /// <summary>
    /// Initialise the minimap from a 3-D tile grid, sampling the given floor.
    /// </summary>
    public void InitFromGrid3D(TileCell[,,] grid, byte floorZ)
    {
        int w = grid.GetLength(0);
        int h = grid.GetLength(1);
        int z = Math.Clamp(floorZ, 0, grid.GetLength(2) - 1);

        MapWidth  = w;
        MapHeight = h;
        _colors   = new Color[w, h];
        _explored = new bool[w, h];

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            _colors[x, y] = MinimapColor(grid[x, y, z]);
    }

    /// <summary>
    /// Initialise the minimap from a 2-D tile grid.
    /// </summary>
    public void InitFromGrid(TileCell[,] grid)
    {
        int w = grid.GetLength(0);
        int h = grid.GetLength(1);

        MapWidth  = w;
        MapHeight = h;
        _colors   = new Color[w, h];
        _explored = new bool[w, h];

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            _colors[x, y] = MinimapColor(grid[x, y]);
    }

    // ── Runtime updates ───────────────────────────────────────────────────

    /// <summary>
    /// Mark all tiles in the closed rectangle [minTX..maxTX, minTY..maxTY] as
    /// explored.  Out-of-bounds coordinates are clamped silently.
    /// </summary>
    public void MarkVisible(int minTX, int minTY, int maxTX, int maxTY)
    {
        if (_explored == null) return;
        int x0 = Math.Max(0,          minTX);
        int y0 = Math.Max(0,          minTY);
        int x1 = Math.Min(MapWidth  - 1, maxTX);
        int y1 = Math.Min(MapHeight - 1, maxTY);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            _explored[x, y] = true;
    }

    // ── Query ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Try to read the minimap state for a tile.
    /// Returns false when no map has been loaded or the coordinates are
    /// out of range.
    /// </summary>
    public bool TryGetTile(int x, int y, out Color color, out bool explored)
    {
        if (_colors == null || x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
        {
            color    = Color.Black;
            explored = false;
            return false;
        }
        color    = _colors[x, y];
        explored = _explored![x, y];
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Derive a compact minimap colour from a tile cell.
    /// The colour is darkened slightly so it is distinct from full-brightness
    /// in-world tiles.
    /// </summary>
    private static Color MinimapColor(TileCell cell)
    {
        var c = cell.GroundColor;
        return new Color(
            (byte)((float)c.R * 0.70f),
            (byte)((float)c.G * 0.70f),
            (byte)((float)c.B * 0.70f),
            (byte)255);
    }
}
