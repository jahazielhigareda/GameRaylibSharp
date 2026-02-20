using Client.ECS.Components;
using Raylib_cs;
using Shared;

namespace Client.ECS.Systems;

public class RenderSystem : ISystem
{
    private readonly World            _world;
    private readonly BackgroundSystem _backgroundSystem;

    public RenderSystem(World world, BackgroundSystem backgroundSystem)
    {
        _world            = world;
        _backgroundSystem = backgroundSystem;
    }

    public void Update(float deltaTime)
    {
        var (offsetX, offsetY) = _backgroundSystem.GetCameraOffset();
        int ts = Constants.TileSize;

        foreach (var entity in _world.GetEntitiesWith<RenderComponent>())
        {
            var pos    = entity.GetComponent<PositionComponent>();
            var render = entity.GetComponent<RenderComponent>();

            // Centrar el sprite dentro del tile
            int drawX = (int)(pos.X + offsetX + (ts - render.Size) / 2f);
            int drawY = (int)(pos.Y + offsetY + (ts - render.Size) / 2f);

            Raylib.DrawRectangle(drawX, drawY, render.Size, render.Size, render.Color);

            // Dibujar borde para mejor visibilidad
            Raylib.DrawRectangleLines(drawX, drawY, render.Size, render.Size,
                entity.HasComponent<LocalPlayerComponent>()
                    ? new Color(100, 100, 255, 255)
                    : new Color(255, 100, 100, 255));
        }
    }
}
