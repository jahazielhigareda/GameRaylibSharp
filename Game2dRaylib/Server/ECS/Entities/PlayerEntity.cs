using Server.ECS.Components;

namespace Server.ECS.Entities;

public class PlayerEntity : Entity
{
    public PlayerEntity(int networkId, int startTileX, int startTileY)
    {
        AddComponent(new NetworkIdComponent { Id = networkId });

        var pos = new PositionComponent();
        pos.SetTilePosition(startTileX, startTileY);
        AddComponent(pos);

        AddComponent(new SpeedComponent());
        AddComponent(new MovementQueueComponent());
    }
}
