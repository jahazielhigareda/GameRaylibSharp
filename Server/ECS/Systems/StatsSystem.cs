using Server.Events;
using Server.ECS;
using Server.ECS.Components;

namespace Server.ECS.Systems;

/// <summary>
/// Arch-based stats system. Handles HP/MP regeneration for all entities with stats.
/// </summary>
public class StatsSystem : ISystem
{
    private readonly ServerWorld _world;
    private readonly EventBus    _eventBus;

    public StatsSystem(ServerWorld world, EventBus eventBus)
    {
        _world    = world;
        _eventBus = eventBus;
    }

    public void Update(float deltaTime)
    {
        _world.ForEachPlayer((
            ref NetworkIdComponent nid,
            ref StatsComponent stats,
            ref SpeedComponent spd,
            ref SkillsComponent skills) =>
        {
            int levelBefore = stats.Level;
            stats.Regenerate(deltaTime);
            if (stats.Level > levelBefore)
            {
                _eventBus.Publish(new PlayerLevelUp
                {
                    PlayerId = nid.Id,
                    NewLevel = stats.Level,
                    NewMaxHP = stats.MaxHP,
                    NewMaxMP = stats.MaxMP
                });
            }
        });
    }
}
