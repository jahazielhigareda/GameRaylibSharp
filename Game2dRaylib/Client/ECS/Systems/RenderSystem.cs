using Arch.Core;
using Arch.Core.Extensions;
using Client.ECS.Components;
using Raylib_cs;
using Shared;

namespace Client.ECS.Systems;

/// <summary>
/// Arch-based render system.
/// Renders all entities with PositionComponent + RenderComponent inside the viewport.
/// </summary>
public class RenderSystem : ISystem
{
    private readonly ClientWorld      _world;
    private readonly BackgroundSystem _bg;

    public RenderSystem(ClientWorld world, BackgroundSystem bg)
    {
        _world = world;
        _bg    = bg;
    }

    public void Update(float deltaTime)
    {
        var (offsetX, offsetY) = _bg.GetCameraOffset();
        int ts = Constants.TileSize;

        _world.ForEachRenderable((Entity entity,
                                  ref PositionComponent pos,
                                  ref RenderComponent render) =>
        {
            // Skip entities managed by CreatureRenderSystem (avoids double draw)
            if (entity.Has<CreatureRenderOrder>()) return;

            // Frustum culling by tile
            if (pos.TileX < _bg.VisibleMinTileX - 1 ||
                pos.TileX > _bg.VisibleMaxTileX + 1 ||
                pos.TileY < _bg.VisibleMinTileY - 1 ||
                pos.TileY > _bg.VisibleMaxTileY + 1)
                return;

            int drawX = (int)(pos.X + offsetX + (ts - render.Size) / 2f);
            int drawY = (int)(pos.Y + offsetY + (ts - render.Size) / 2f);

            Raylib.DrawRectangle(drawX, drawY, render.Size, render.Size, render.Color);

            bool isLocal = entity.Has<LocalPlayerComponent>();
            Raylib.DrawRectangleLines(drawX, drawY, render.Size, render.Size,
                isLocal ? new Color(100, 100, 255, 255)
                        : new Color(255, 100, 100, 255));
        });
    }
}
