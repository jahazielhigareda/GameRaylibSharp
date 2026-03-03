using Arch.Core;
using Arch.Core.Extensions;
using Client.ECS.Components;
using Client.Services;
using Raylib_cs;

namespace Client.ECS.Systems;

/// <summary>
/// Renders a Tibia-style minimap widget in the bottom-right corner of the screen.
///
/// Layout (3 px per tile, 50×38 map → 150×114 px tile area):
/// ┌─────────────────────────────┐
/// │ MINIMAP                  M │  ← 16-px title bar
/// ├─────────────────────────────┤
/// │  [explored tiles]           │
/// │  · creature (red)           │
/// │  ■ player (white)           │
/// │  · other player (green)     │
/// └─────────────────────────────┘
///
/// Toggle visibility with the <b>M</b> key (Tibia default).
/// The widget is hidden until the first map packet arrives.
/// </summary>
public class MinimapSystem : ISystem
{
    private const int TilePx    = 3;   // pixels per minimap tile
    private const int PadPx     = 4;   // inner padding inside the border
    private const int TitleH    = 16;  // title bar height in pixels
    private const int MarginPx  = 10;  // margin from screen edge

    private readonly MinimapService  _minimap;
    private readonly ClientWorld     _world;
    private readonly GameStateService _state;

    private bool _visible = true;

    // Arch queries
    private static readonly QueryDescription LocalQuery = new QueryDescription()
        .WithAll<LocalPlayerComponent, PositionComponent>();

    private static readonly QueryDescription CreatureQuery = new QueryDescription()
        .WithAll<CreatureClientTag, PositionComponent>();

    private static readonly QueryDescription RemotePlayerQuery = new QueryDescription()
        .WithAll<NetworkIdComponent, PositionComponent, RenderComponent>()
        .WithNone<LocalPlayerComponent, CreatureClientTag>();

    public MinimapSystem(MinimapService minimap, ClientWorld world, GameStateService state)
    {
        _minimap = minimap;
        _world   = world;
        _state   = state;
    }

    public void Update(float deltaTime)
    {
        // Toggle with M
        if (Raylib.IsKeyPressed(KeyboardKey.M))
            _visible = !_visible;

        if (!_visible || !_minimap.HasMap) return;

        int mapW = _minimap.MapWidth;
        int mapH = _minimap.MapHeight;

        int tileAreaW = mapW * TilePx;
        int tileAreaH = mapH * TilePx;
        int widgetW   = tileAreaW + PadPx * 2;
        int widgetH   = tileAreaH + PadPx * 2 + TitleH;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        int wx = sw - widgetW - MarginPx;
        int wy = sh - widgetH - MarginPx;

        // ── Background panel ──────────────────────────────────────────────
        Raylib.DrawRectangle(wx, wy, widgetW, widgetH, new Color(0, 0, 0, 200));
        Raylib.DrawRectangleLines(wx, wy, widgetW, widgetH, new Color(120, 120, 120, 255));

        // ── Title bar ─────────────────────────────────────────────────────
        Raylib.DrawRectangle(wx + 1, wy + 1, widgetW - 2, TitleH - 1,
            new Color(40, 40, 60, 220));
        Raylib.DrawText("Minimap", wx + 4, wy + 2, 12, Color.Gold);
        Raylib.DrawText("[M]", wx + widgetW - 30, wy + 2, 12, Color.Gray);

        // ── Tile origin in screen space ───────────────────────────────────
        int tileOriginX = wx + PadPx;
        int tileOriginY = wy + TitleH + PadPx;

        // ── Draw explored tiles ───────────────────────────────────────────
        for (int ty = 0; ty < mapH; ty++)
        for (int tx = 0; tx < mapW; tx++)
        {
            if (!_minimap.TryGetTile(tx, ty, out Color color, out bool explored)) continue;

            int px = tileOriginX + tx * TilePx;
            int py = tileOriginY + ty * TilePx;

            if (explored)
                Raylib.DrawRectangle(px, py, TilePx, TilePx, color);
            else
                Raylib.DrawRectangle(px, py, TilePx, TilePx, new Color(8, 8, 8, 255));
        }

        // ── Creature dots (red) ───────────────────────────────────────────
        _world.World.Query(in CreatureQuery, (ref PositionComponent pos) =>
        {
            DrawDot(tileOriginX, tileOriginY, pos.TileX, pos.TileY, mapW, mapH,
                new Color(220, 40, 40, 255));
        });

        // ── Remote-player dots (green) ────────────────────────────────────
        _world.World.Query(in RemotePlayerQuery, (ref PositionComponent pos) =>
        {
            DrawDot(tileOriginX, tileOriginY, pos.TileX, pos.TileY, mapW, mapH,
                new Color(40, 220, 40, 255));
        });

        // ── Local player dot (white) ──────────────────────────────────────
        _world.World.Query(in LocalQuery, (ref PositionComponent pos) =>
        {
            DrawDot(tileOriginX, tileOriginY, pos.TileX, pos.TileY, mapW, mapH,
                Color.White);
        });
    }

    // ── Helper ────────────────────────────────────────────────────────────

    private static void DrawDot(int originX, int originY,
                                int tileX, int tileY,
                                int mapW,  int mapH,
                                Color color)
    {
        if (tileX < 0 || tileX >= mapW || tileY < 0 || tileY >= mapH) return;
        int px = originX + tileX * TilePx;
        int py = originY + tileY * TilePx;
        Raylib.DrawRectangle(px, py, TilePx, TilePx, color);
    }
}
