using Client.Services;
using Raylib_cs;
using Shared;

namespace Client.ECS.Systems;

/// <summary>
/// Renders tile layers 0-2 and 4-5 (everything except creatures) using
/// Tibia's Painter's Algorithm: NW→SE, layer 0 first, layer 5 last.
///
/// The map is supplied via SetMapGrid() called once after the client
/// receives or loads map data.  Until then the system falls back to the
/// legacy solid-colour grid.
///
/// Layer order:
///   0 GROUND      – grass / water / sand / stone colours
///   1 BORDER      – thin edge accent lines between terrain transitions
///   2 BOTTOM ITEM – small decor dot (rocks, bushes)
///   3 CREATURE    – skipped here; handled by CreatureRenderSystem
///   4 TOP ITEM    – trees / walls drawn as taller rectangles
///   5 EFFECT      – tile-bound glow overlay
/// </summary>
public class TileRenderSystem : ISystem
{
    private readonly ClientWorld     _world;
    private readonly CameraService   _camera;

    // Optional map grid – null until server sends map info
    private TileCell[,]? _grid;
    private int           _gridW, _gridH;

    public TileRenderSystem(ClientWorld world, CameraService camera)
    {
        _world  = world;
        _camera = camera;
    }

    /// <summary>Called by ClientNetworkManager when MapData arrives.</summary>
    public void SetMapGrid(TileCell[,] grid)
    {
        _grid  = grid;
        _gridW = grid.GetLength(0);
        _gridH = grid.GetLength(1);
    }

    public void Update(float deltaTime)
    {
        var (offsetX, offsetY) = _camera.GetOffset();
        int ts   = Constants.TileSize;
        int sw   = Raylib.GetScreenWidth();
        int sh   = Raylib.GetScreenHeight();

        int minTX = Math.Max(0, (int)Math.Floor(-offsetX / ts) - 1);
        int minTY = Math.Max(0, (int)Math.Floor(-offsetY / ts) - 1);
        int maxTX = (int)Math.Floor((-offsetX + sw) / ts) + 1;
        int maxTY = (int)Math.Floor((-offsetY + sh) / ts) + 1;

        if (_grid != null)
        {
            maxTX = Math.Min(maxTX, _gridW - 1);
            maxTY = Math.Min(maxTY, _gridH - 1);
        }

        // NW → SE scan
        for (int ty = minTY; ty <= maxTY; ty++)
        for (int tx = minTX; tx <= maxTX; tx++)
        {
            int sx = (int)(tx * ts + offsetX);
            int sy = (int)(ty * ts + offsetY);

            if (_grid != null && tx < _gridW && ty < _gridH)
                DrawLayered(sx, sy, ts, _grid[tx, ty]);
            else
                DrawFallback(sx, sy, ts, tx, ty);
        }
    }

    // ── Layered draw ──────────────────────────────────────────────────────

    private static void DrawLayered(int sx, int sy, int ts, in TileCell cell)
    {
        // Layer 0 – GROUND
        Raylib.DrawRectangle(sx, sy, ts, ts, cell.GroundColor);

        // Layer 1 – BORDER accent (thin 1-px inset rect if border tile)
        if (cell.HasBorder)
            Raylib.DrawRectangleLines(sx + 1, sy + 1, ts - 2, ts - 2,
                ColorAlpha(cell.GroundColor, 0.45f));

        // Layer 2 – BOTTOM ITEM (small centred dot)
        if (cell.BottomItemId != 0)
        {
            int r = ts / 8;
            Raylib.DrawRectangle(sx + ts/2 - r, sy + ts/2 - r, r*2, r*2,
                new Color(160, 130, 80, 200));
        }

        // Layer 4 – TOP ITEM (tree / wall: taller rectangle)
        if (cell.TopItemId != 0)
        {
            int tw = ts - 6;
            int th = (int)(ts * 1.4f);
            Raylib.DrawRectangle(sx + 3, sy - th + ts, tw, th, cell.TopItemColor);
            Raylib.DrawRectangleLines(sx + 3, sy - th + ts, tw, th,
                ColorAlpha(cell.TopItemColor, 0.5f));
        }

        // Layer 5 – EFFECT glow overlay
        if (cell.EffectId != 0)
            Raylib.DrawRectangle(sx, sy, ts, ts, new Color(255, 255, 100, 40));

        // grid line
        Raylib.DrawRectangleLines(sx, sy, ts, ts, new Color(60, 60, 60, 120));
    }

    private static void DrawFallback(int sx, int sy, int ts, int tx, int ty)
    {
        // Legacy: border = wall (gray), interior = walkable (dark)
        bool isWall = tx == 0 || ty == 0;
        var  col    = isWall
            ? new Color(30, 30, 30, 255)
            : new Color(60, 60, 60, 255);
        Raylib.DrawRectangle(sx, sy, ts, ts, col);
        Raylib.DrawRectangleLines(sx, sy, ts, ts, new Color(80, 80, 80, 255));
    }

    private static Color ColorAlpha(Color c, float a)
        => new Color(c.R, c.G, c.B, (byte)(a * 255));
}

/// <summary>
/// Lightweight value type representing one cell in the client tile grid.
/// Populated from MapData once map arrives; updated incrementally on changes.
/// </summary>
public struct TileCell
{
    public ushort GroundId;
    public Color  GroundColor;
    public bool   HasBorder;
    public ushort BottomItemId;
    public ushort TopItemId;
    public Color  TopItemColor;
    public ushort EffectId;
    public bool   IsWalkable;

    /// <summary>Build a TileCell from a ground item id.</summary>
    public static TileCell FromGroundId(ushort groundId, ushort flags)
    {
        bool walkable = (flags & 0x0001) != 0;
        bool blockProj = (flags & 0x0002) != 0;

        Color groundColor;
        ushort topItemId  = 0;
        Color  topColor   = default;

        switch (groundId)
        {
            case 1231: groundColor = new Color(34,  139, 34,  255); break; // Grass
            case 1055: groundColor = new Color(105, 105, 105, 255); break; // Stone
            case 4608: groundColor = new Color(0,   0,   139, 255); break; // Water
            case 231:  groundColor = new Color(194, 178, 128, 255); break; // Sand
            case 2700:                                                       // Tree
                groundColor = new Color(34, 100, 34, 255);
                topItemId   = 2700;
                topColor    = new Color(0,  80,  0,  255);
                break;
            case 1:    groundColor = new Color(50,  50,  50,  255); break; // Wall
            default:   groundColor = new Color(60,  60,  60,  255); break;
        }

        return new TileCell
        {
            GroundId     = groundId,
            GroundColor  = groundColor,
            HasBorder    = !walkable && groundId != 1,
            TopItemId    = topItemId,
            TopItemColor = topColor,
            IsWalkable   = walkable,
        };
    }
}
