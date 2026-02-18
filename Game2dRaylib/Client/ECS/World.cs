using Client.ECS.Entities;

namespace Client.ECS;

public class World
{
    private readonly List<Entity> _entities = new();

    public IReadOnlyList<Entity> Entities => _entities;

    public void AddEntity(Entity e)    => _entities.Add(e);
    public void RemoveEntity(Entity e) => _entities.Remove(e);

    public IEnumerable<Entity> GetEntitiesWith<T>() where T : class
        => _entities.Where(e => e.HasComponent<T>());
}
