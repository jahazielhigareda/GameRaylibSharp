using Client.ECS.Components;
using Raylib_cs;
using Shared;

namespace Client.ECS.Entities;

public class PlayerEntity : Entity
{
    public PlayerEntity(int networkId, bool isLocal)
    {
        AddComponent(new NetworkIdComponent { Id = networkId });
        AddComponent(new PositionComponent());
        AddComponent(new RenderComponent
        {
            Color = isLocal ? Color.Blue : Color.Red,
            Size  = Constants.PlayerSize
        });
        if (isLocal)
        {
            AddComponent(new LocalPlayerComponent());
            AddComponent(new StatsDataComponent());
            AddComponent(new SkillsDataComponent());
        }
    }
}
