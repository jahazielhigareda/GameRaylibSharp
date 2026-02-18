using Server.ECS.Entities;

namespace Server.ECS;

public class World
{
    private readonly List<Entity> _entities = new();

    public IReadOnlyList<Entity> Entities => _entities;

    public void AddEntity(Entity entity) => _entities.Add(entity);

    public void RemoveEntity(Entity entity) => _entities.Remove(entity);

    public IEnumerable<Entity> GetEntitiesWith<T>() where T : class
        => _entities.Where(e => e.HasComponent<T>());
}
