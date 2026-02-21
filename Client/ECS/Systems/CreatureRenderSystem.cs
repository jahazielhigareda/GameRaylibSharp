using Arch.Core;
using Arch.Core.Extensions;
using Client.ECS;
using Client.ECS.Components;
using Client.Services;
using Raylib_cs;
using Shared;

namespace Client.ECS.Systems;

/// <summary>
/// Layer 3 – renders all creatures and players sorted by Y position.
///
/// Features:
///   • Tile-outline target indicator (yellow square around full tile, Tibia style).
///   • Creature sprite + coloured border.
///   • Health bar above creature.
///   • Creature name tag above health bar.
/// </summary>
public class CreatureRenderSystem : ISystem
{
    private readonly ClientWorld      _world;
    private readonly CameraService    _camera;
    private readonly GameStateService _state;

    private static readonly QueryDescription RenderQuery = new QueryDescription()
        .WithAll<PositionComponent, RenderComponent, CreatureRenderOrder>();

    private readonly List<(float y, Action draw)> _drawCalls = new(64);

    public CreatureRenderSystem(ClientWorld world, CameraService camera, GameStateService state)
    {
        _world  = world;
        _camera = camera;
        _state  = state;
    }

    public void Update(float deltaTime)
    {
        var (offsetX, offsetY) = _camera.GetOffset();
        int ts       = Constants.TileSize;
        int targetId = _state.TargetedEntityId;

        _drawCalls.Clear();

        _world.World.Query(in RenderQuery,
            (Entity entity,
             ref PositionComponent   pos,
             ref RenderComponent     render,
             ref CreatureRenderOrder order) =>
        {
            float drawX = pos.X + offsetX + (ts - render.Size) / 2f;
            float drawY = pos.Y + offsetY + (ts - render.Size) / 2f;
            order.YSortKey = pos.Y;

            bool isLocal    = entity.Has<LocalPlayerComponent>();
            bool isCreature = entity.Has<CreatureClientTag>();
            bool isTargeted = isCreature
                           && entity.Has<NetworkIdComponent>()
                           && entity.Get<NetworkIdComponent>().Id == targetId
                           && targetId != 0;

            byte hpPct = 100;
            if (entity.Has<CreatureHpComponent>())
                hpPct = entity.Get<CreatureHpComponent>().HpPct;

            // Name (creatures only)
            string name = string.Empty;
            if (isCreature && entity.Has<CreatureNameComponent>())
                name = entity.Get<CreatureNameComponent>().Name ?? string.Empty;

            // Tile pixel origin for target square
            float tileScreenX = pos.TileX * ts + offsetX;
            float tileScreenY = pos.TileY * ts + offsetY;

            var body   = render.Color;
            var border = isLocal    ? new Color(100, 100, 255, 255)
                       : isTargeted ? new Color(255,  30,  30, 255)
                       : isCreature ? new Color(255, 140,   0, 255)
                       :              new Color(255, 100, 100, 255);

            int   size   = render.Size;
            int   idx    = (int)drawX;
            int   idy    = (int)drawY;
            float sortY  = order.YSortKey;

            // Capture for closure
            bool   cIsCreature = isCreature;
            bool   cIsTargeted = isTargeted;
            int    cTileX      = (int)tileScreenX;
            int    cTileY      = (int)tileScreenY;
            byte   cHpPct      = hpPct;
            string cName       = name;
            int    cTs         = ts;

            _drawCalls.Add((sortY, () =>
            {
                // ── 1. Tile target square (drawn BEFORE sprite) ───────────
                if (cIsTargeted)
                {
                    Raylib.DrawRectangleLines(cTileX,     cTileY,     cTs,     cTs,     new Color(255, 220,   0, 230));
                    Raylib.DrawRectangleLines(cTileX + 1, cTileY + 1, cTs - 2, cTs - 2, new Color(255, 255, 100, 150));
                    Raylib.DrawRectangleLines(cTileX + 2, cTileY + 2, cTs - 4, cTs - 4, new Color(255, 200,   0, 100));
                }

                // ── 2. Sprite body + border ───────────────────────────────
                Raylib.DrawRectangle(idx, idy, size, size, body);
                Raylib.DrawRectangleLines(idx, idy, size, size, border);

                if (cIsTargeted)
                {
                    Raylib.DrawRectangleLines(idx - 1, idy - 1, size + 2, size + 2, border);
                    Raylib.DrawRectangleLines(idx - 2, idy - 2, size + 4, size + 4,
                                              new Color(255, 0, 0, 160));
                }

                // ── 3. Health bar + name (creatures only) ─────────────────
                if (cIsCreature)
                {
                    int barW  = size;
                    int barH  = 4;
                    int barX  = idx;
                    int barY  = idy - barH - 2;
                    int fillW = (int)(barW * cHpPct / 100f);

                    var bgCol   = new Color(60, 0, 0, 200);
                    var fillCol = cHpPct > 50 ? new Color(0, 200,  0, 255)
                                : cHpPct > 25 ? new Color(220, 200, 0, 255)
                                :               new Color(220,  30, 30, 255);

                    Raylib.DrawRectangle(barX, barY, barW, barH, bgCol);
                    Raylib.DrawRectangle(barX, barY, fillW, barH, fillCol);

                    // ── Name tag above health bar ─────────────────────────
                    if (!string.IsNullOrEmpty(cName))
                    {
                        const int fontSize = 12;
                        int nameW = Raylib.MeasureText(cName, fontSize);
                        int nameX = idx + size / 2 - nameW / 2;
                        int nameY = barY - fontSize - 2;

                        // Drop shadow
                        Raylib.DrawText(cName, nameX + 1, nameY + 1, fontSize,
                                        new Color(0, 0, 0, 210));

                        // Name: red when targeted, white otherwise
                        var nameCol = cIsTargeted
                            ? new Color(255, 80, 80, 255)
                            : new Color(255, 255, 255, 230);

                        Raylib.DrawText(cName, nameX, nameY, fontSize, nameCol);
                    }
                }
            }));
        });

        // Y-sort painter's algorithm
        _drawCalls.Sort(static (a, b) => a.y.CompareTo(b.y));
        foreach (var (_, draw) in _drawCalls)
            draw();
    }
}
