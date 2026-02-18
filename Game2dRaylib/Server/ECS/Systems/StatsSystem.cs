using Server.ECS;
using Server.ECS.Components;

namespace Server.ECS.Systems;

/// <summary>
/// Arch-based stats system. Handles HP/MP regeneration for all entities with stats.
/// </summary>
public class StatsSystem : ISystem
{
    private readonly ServerWorld _world;

    public StatsSystem(ServerWorld world) => _world = world;

    public void Update(float deltaTime)
    {
        _world.ForEachStats((ref StatsComponent stats) =>
        {
            stats.Regenerate(deltaTime);
        });
    }
}
