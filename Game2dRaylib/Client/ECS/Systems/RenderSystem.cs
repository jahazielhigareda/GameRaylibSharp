using Client.ECS.Components;
using Client.ECS.Systems;
using Raylib_cs;

namespace Client.ECS.Systems;

public class RenderSystem : ISystem
{
    private readonly World _world;

    public RenderSystem(World world) => _world = world;

    public void Update(float deltaTime)
    {
        foreach (var entity in _world.GetEntitiesWith<RenderComponent>())
        {
            var pos    = entity.GetComponent<PositionComponent>();
            var render = entity.GetComponent<RenderComponent>();
            Raylib.DrawRectangle((int)pos.X - render.Size / 2, (int)pos.Y - render.Size / 2,
                                  render.Size, render.Size, render.Color);
        }
    }
}
