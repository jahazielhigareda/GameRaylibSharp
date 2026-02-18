namespace Server.ECS.Entities;

public class Entity
{
    private static int _nextId = 1;

    public int Id { get; } = _nextId++;
    private readonly Dictionary<Type, object> _components = new();

    public void AddComponent<T>(T component) where T : class
        => _components[typeof(T)] = component;

    public T GetComponent<T>() where T : class
        => (T)_components[typeof(T)];

    public bool HasComponent<T>() where T : class
        => _components.ContainsKey(typeof(T));
}
