using Client.ECS.Components;

namespace Client.ECS.Systems;

/// <summary>
/// Arch-based interpolation system.
/// Smoothly lerps visual positions toward server-authoritative tile positions.
/// </summary>
public class InterpolationSystem : ISystem
{
    private readonly ClientWorld _world;

    public InterpolationSystem(ClientWorld world) => _world = world;

    public void Update(float deltaTime)
    {
        _world.ForEachInterpolation((ref PositionComponent pos) =>
        {
            pos.Interpolate(deltaTime);
        });
    }
}
