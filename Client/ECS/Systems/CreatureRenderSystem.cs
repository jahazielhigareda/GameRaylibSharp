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
/// Highlights the targeted creature with a bright-red outline and
/// draws a health bar above every creature entity.
/// </summary>
public class CreatureRenderSystem : ISystem
{
    private readonly ClientWorld   _world;
    private readonly CameraService _camera;
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
        int ts = Constants.TileSize;
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
            bool isTargeted = isCreature && entity.Has<NetworkIdComponent>()
                           && entity.Get<NetworkIdComponent>().Id == targetId
                           && targetId != 0;

            byte hpPct = 100;
            if (entity.Has<CreatureHpComponent>())
                hpPct = entity.Get<CreatureHpComponent>().HpPct;

            var  body   = render.Color;
            var  border = isLocal    ? new Color(100, 100, 255, 255)
                        : isTargeted ? new Color(255, 30,  30,  255)
                        : isCreature ? new Color(255, 140,  0,  255)
                        :              new Color(255, 100, 100, 255);
            int  size   = render.Size;
            int  idx    = (int)drawX;
            int  idy    = (int)drawY;
            int  borderW = isTargeted ? 3 : 1;
            float sortY  = order.YSortKey;

            _drawCalls.Add((sortY, () =>
            {
                Raylib.DrawRectangle(idx, idy, size, size, body);
                Raylib.DrawRectangleLines(idx, idy, size, size, border);

                // Thicker border when targeted
                if (isTargeted)
                {
                    Raylib.DrawRectangleLines(idx - 1, idy - 1, size + 2, size + 2, border);
                    Raylib.DrawRectangleLines(idx - 2, idy - 2, size + 4, size + 4,
                                              new Color(255, 0, 0, 160));
                }

                // Health bar above creature
                if (isCreature)
                {
                    int barW   = size;
                    int barH   = 4;
                    int barX   = idx;
                    int barY   = idy - barH - 2;
                    int fillW  = (int)(barW * hpPct / 100f);
                    var bgCol  = new Color(60, 0, 0, 200);
                    var fillCol= hpPct > 50 ? new Color(0, 200, 0, 255)
                               : hpPct > 25 ? new Color(220, 200, 0, 255)
                               :              new Color(220, 30, 30, 255);

                    Raylib.DrawRectangle(barX, barY, barW, barH, bgCol);
                    Raylib.DrawRectangle(barX, barY, fillW, barH, fillCol);
                }
            }));
        });

        _drawCalls.Sort(static (a, b) => a.y.CompareTo(b.y));
        foreach (var (_, draw) in _drawCalls)
            draw();
    }
}
