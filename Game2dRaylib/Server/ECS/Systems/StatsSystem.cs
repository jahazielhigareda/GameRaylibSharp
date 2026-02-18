using Server.ECS.Components;

namespace Server.ECS.Systems;

/// <summary>
/// Sistema que maneja la regeneración de HP/MP y otros procesos de stats.
/// </summary>
public class StatsSystem : ISystem
{
    private readonly World _world;

    public StatsSystem(World world) => _world = world;

    public void Update(float deltaTime)
    {
        foreach (var entity in _world.GetEntitiesWith<StatsComponent>())
        {
            var stats = entity.GetComponent<StatsComponent>();
            stats.Regenerate(deltaTime);
        }
    }
}
