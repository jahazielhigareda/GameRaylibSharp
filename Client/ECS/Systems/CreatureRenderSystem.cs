using Client.Services;
using Arch.Core;
using Arch.Core.Extensions;
using Client.ECS.Components;
using Raylib_cs;
using Shared;

namespace Client.ECS.Systems;

/// <summary>
/// Layer 3 – renders all creatures and players sorted by Y position
/// (painter's algorithm: entities further north drawn first).
/// </summary>
public class CreatureRenderSystem : ISystem
{
    private readonly ClientWorld   _world;
    private readonly CameraService _camera;

    private static readonly QueryDescription RenderQuery = new QueryDescription()
        .WithAll<PositionComponent, RenderComponent, CreatureRenderOrder>();

    // Reusable sort buffer – avoids per-frame allocation after warm-up
    private readonly List<(float y, Action draw)> _drawCalls = new(64);

    public CreatureRenderSystem(ClientWorld world, CameraService camera)
    {
        _world  = world;
        _camera = camera;
    }

    public void Update(float deltaTime)
    {
        var (offsetX, offsetY) = _camera.GetOffset();
        int ts = Constants.TileSize;

        _drawCalls.Clear();

        _world.World.Query(in RenderQuery,
            (Entity entity,
             ref PositionComponent   pos,
             ref RenderComponent     render,
             ref CreatureRenderOrder order) =>
        {
            float drawX = pos.X + offsetX + (ts - render.Size) / 2f;
            float drawY = pos.Y + offsetY + (ts - render.Size) / 2f;

            // Update sort key from current visual position
            order.YSortKey = pos.Y;

            bool isLocal = entity.Has<LocalPlayerComponent>();
            var  body    = render.Color;
            var  border  = isLocal
                ? new Color(100, 100, 255, 255)
                : new Color(255, 100, 100, 255);
            int  size    = render.Size;
            int  idx     = (int)drawX;
            int  idy     = (int)drawY;

            float sortY = order.YSortKey;
            _drawCalls.Add((sortY, () =>
            {
                Raylib.DrawRectangle(idx, idy, size, size, body);
                Raylib.DrawRectangleLines(idx, idy, size, size, border);
            }));
        });

        // Sort back → front (ascending Y = further away drawn first)
        _drawCalls.Sort(static (a, b) => a.y.CompareTo(b.y));

        foreach (var (_, draw) in _drawCalls)
            draw();
    }
}
