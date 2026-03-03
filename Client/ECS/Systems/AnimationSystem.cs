using Arch.Core;
using Client.ECS.Components;

namespace Client.ECS.Systems;

/// <summary>
/// Updates <see cref="AnimationComponent"/> for every entity that has one.
/// Runs after InterpolationSystem so the visual position is already up to date.
/// </summary>
public class AnimationSystem : ISystem
{
    private readonly ClientWorld _world;

    private static readonly QueryDescription AnimQuery = new QueryDescription()
        .WithAll<PositionComponent, AnimationComponent>();

    public AnimationSystem(ClientWorld world) => _world = world;

    public void Update(float deltaTime)
    {
        _world.World.Query(in AnimQuery,
            (ref PositionComponent pos, ref AnimationComponent anim) =>
            {
                anim.Update(deltaTime, ref pos);
            });
    }
}
