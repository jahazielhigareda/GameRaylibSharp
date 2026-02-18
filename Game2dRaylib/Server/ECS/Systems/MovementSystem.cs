using Server.ECS.Components;
using Server.ECS.Systems;
using Shared;

namespace Server.ECS.Systems;

public class MovementSystem : ISystem
{
    private readonly World _world;

    public MovementSystem(World world)
        => _world = world;

    public void Update(float deltaTime)
    {
        foreach (var entity in _world.GetEntitiesWith<InputComponent>())
        {
            var input = entity.GetComponent<InputComponent>();
            var vel   = entity.GetComponent<VelocityComponent>();

            vel.Vx = 0;
            vel.Vy = 0;

            if (input.Left)  vel.Vx -= Constants.PlayerSpeed;
            if (input.Right) vel.Vx += Constants.PlayerSpeed;
            if (input.Up)    vel.Vy -= Constants.PlayerSpeed;
            if (input.Down)  vel.Vy += Constants.PlayerSpeed;

            var pos = entity.GetComponent<PositionComponent>();
            pos.X += vel.Vx * deltaTime;
            pos.Y += vel.Vy * deltaTime;
        }
    }
}
