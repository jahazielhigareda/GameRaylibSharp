using Client.ECS.Components;

namespace Client.ECS.Systems;

/// <summary>
/// Interpola suavemente la posición visual de todas las entidades
/// hacia su posición objetivo (recibida del servidor).
/// Esto produce el efecto de "smooth walking" estilo Tibia.
/// </summary>
public class InterpolationSystem : ISystem
{
    private readonly World _world;

    public InterpolationSystem(World world) => _world = world;

    public void Update(float deltaTime)
    {
        foreach (var entity in _world.GetEntitiesWith<PositionComponent>())
        {
            var pos = entity.GetComponent<PositionComponent>();
            pos.Interpolate(deltaTime);
        }
    }
}
