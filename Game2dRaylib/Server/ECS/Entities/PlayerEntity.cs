using Server.ECS.Components;
using Server.ECS.Entities;

namespace Server.ECS.Entities;

public class PlayerEntity : Entity
{
    public PlayerEntity(int networkId, float startX, float startY)
    {
        AddComponent(new NetworkIdComponent { Id = networkId });
        AddComponent(new PositionComponent  { X = startX, Y = startY });
        AddComponent(new VelocityComponent  { Vx = 0, Vy = 0 });
        AddComponent(new InputComponent());
    }
}
