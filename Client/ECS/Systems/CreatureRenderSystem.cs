using Arch.Core;
using Arch.Core.Extensions;
using Client.ECS;
using Client.ECS.Components;
using Client.Services;
using Raylib_cs;
using Shared;

namespace Client.ECS.Systems;

/// <summary>
/// Layer 3 – renders all creatures and players sorted by Y position
/// using the Tibia Painter's Algorithm (NW→SE render order).
///
/// When <see cref="SpriteService"/> is initialised, entities are drawn
/// as animated sprites with Tibia-style outfit colourisation.
/// If <see cref="SpriteService"/> is not yet available the system falls
/// back to the original coloured-rectangle rendering.
///
/// Targeted creatures receive a bright-red outline.
/// A colour-coded health bar is drawn above every creature entity.
/// </summary>
public class CreatureRenderSystem : ISystem
{
    private readonly ClientWorld      _world;
    private readonly CameraService    _camera;
    private readonly GameStateService _state;
    private readonly SpriteService    _sprites;

    private static readonly QueryDescription RenderQuery = new QueryDescription()
        .WithAll<PositionComponent, RenderComponent, CreatureRenderOrder>();

    private readonly List<(float y, Action draw)> _drawCalls = new(64);

    public CreatureRenderSystem(
        ClientWorld world, CameraService camera,
        GameStateService state, SpriteService sprites)
    {
        _world   = world;
        _camera  = camera;
        _state   = state;
        _sprites = sprites;
    }

    public void Update(float deltaTime)
    {
        var (offsetX, offsetY) = _camera.GetOffset();
        int ts       = Constants.TileSize;
        int targetId = _state.TargetedEntityId;
        bool useSprites = _sprites.IsInitialized;

        _drawCalls.Clear();

        _world.World.Query(in RenderQuery,
            (Entity entity,
             ref PositionComponent   pos,
             ref RenderComponent     render,
             ref CreatureRenderOrder order) =>
        {
            // Sprite draw position: align the 32×32 sprite to the tile grid
            int drawX = (int)(pos.X + offsetX);
            int drawY = (int)(pos.Y + offsetY);
            // For rect fallback, centre the smaller PlayerSize rect within the tile
            int rectX = (int)(pos.X + offsetX + (ts - render.Size) / 2f);
            int rectY = (int)(pos.Y + offsetY + (ts - render.Size) / 2f);
            order.YSortKey = pos.Y;

            bool isLocal    = entity.Has<LocalPlayerComponent>();
            bool isCreature = entity.Has<CreatureClientTag>();
            bool isTargeted = isCreature && entity.Has<NetworkIdComponent>()
                           && entity.Get<NetworkIdComponent>().Id == targetId
                           && targetId != 0;

            byte hpPct = 100;
            if (entity.Has<CreatureHpComponent>())
                hpPct = entity.Get<CreatureHpComponent>().HpPct;

            // Capture animation + outfit data (if present) for the lambda closure
            int  cardinalDir = 2;  // South default
            int  walkFrame   = 0;
            bool hasOutfit   = false;
            OutfitComponent outfit = default;
            int  entityType  = isCreature ? 1 : 0;

            if (entity.Has<AnimationComponent>())
            {
                ref var anim = ref entity.Get<AnimationComponent>();
                cardinalDir = anim.CardinalIndex;
                walkFrame   = anim.Frame;
            }

            if (entity.Has<OutfitComponent>())
            {
                outfit    = entity.Get<OutfitComponent>();
                hasOutfit = true;
                entityType = outfit.OutfitId < SpriteService.EntityTypes
                    ? outfit.OutfitId
                    : entityType;
            }

            var  body   = render.Color;
            int  size   = render.Size;

            float sortY    = order.YSortKey;
            bool  doSprite = useSprites;
            int   sd       = cardinalDir;
            int   sf       = walkFrame;
            int   et       = entityType;

            _drawCalls.Add((sortY, () =>
            {
                if (doSprite && hasOutfit)
                {
                    _sprites.DrawSpriteWithOutfit(et, sd, sf, drawX, drawY, in outfit);
                }
                else if (doSprite)
                {
                    _sprites.DrawSprite(et, sd, sf, drawX, drawY, Color.White);
                }
                else
                {
                    // Fallback: coloured rectangle with role-specific border
                    var fallbackBorder = isLocal    ? new Color(100, 100, 255, 255)
                                       : isTargeted ? new Color(255, 30,  30,  255)
                                       : isCreature ? new Color(255, 140,  0,  255)
                                       :              new Color(255, 100, 100, 255);
                    Raylib.DrawRectangle(rectX, rectY, size, size, body);
                    Raylib.DrawRectangleLines(rectX, rectY, size, size, fallbackBorder);
                }

                // Targeted outline (always drawn, over sprites or rects)
                if (isTargeted)
                {
                    var targetBorder = new Color(255, 30, 30, 255);
                    Raylib.DrawRectangleLines(drawX - 1, drawY - 1, ts + 2, ts + 2, targetBorder);
                    Raylib.DrawRectangleLines(drawX - 2, drawY - 2, ts + 4, ts + 4,
                                              new Color(255, 0, 0, 160));
                }

                // Health bar above creature
                if (isCreature)
                {
                    int barW  = ts;
                    int barH  = 4;
                    int barX  = drawX;
                    int barY  = drawY - barH - 2;
                    int fillW = (int)(barW * hpPct / 100f);
                    var bgCol   = new Color(60, 0, 0, 200);
                    var fillCol = hpPct > 50 ? new Color(0, 200, 0, 255)
                                : hpPct > 25 ? new Color(220, 200, 0, 255)
                                :              new Color(220, 30,  30, 255);

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
