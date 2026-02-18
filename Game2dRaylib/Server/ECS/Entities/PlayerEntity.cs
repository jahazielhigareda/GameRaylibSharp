using Server.ECS.Components;
using Shared;

namespace Server.ECS.Entities;

public class PlayerEntity : Entity
{
    public PlayerEntity(int networkId, int startTileX, int startTileY, Vocation vocation = Vocation.None)
    {
        AddComponent(new NetworkIdComponent { Id = networkId });

        var pos = new PositionComponent();
        pos.SetTilePosition(startTileX, startTileY);
        AddComponent(pos);

        AddComponent(new SpeedComponent());
        AddComponent(new MovementQueueComponent());

        // Stats estilo Tibia
        var stats = new StatsComponent();
        stats.Initialize(vocation);
        AddComponent(stats);

        // Skills estilo Tibia
        var skills = new SkillsComponent();
        AddComponent(skills);
    }
}
