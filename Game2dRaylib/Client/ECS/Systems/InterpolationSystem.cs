using Client.ECS.Components;
using Shared;

namespace Client.ECS.Systems;

/// <summary>
/// Interpola suavemente la posición visual de entidades dentro del viewport.
/// Implementa frustum culling: solo interpola entidades cercanas al jugador local.
/// </summary>
public class InterpolationSystem : ISystem
{
    private readonly World _world;

    public InterpolationSystem(World world) => _world = world;

    public void Update(float deltaTime)
    {
        var localPlayer = _world.GetEntitiesWith<LocalPlayerComponent>().FirstOrDefault();
        PositionComponent? localPos = localPlayer?.GetComponent<PositionComponent>();

        // Rango de interpolación (un poco más amplio que el viewport para suavidad)
        int halfViewX = Constants.ViewportTilesX / 2 + Constants.ViewportMargin + 2;
        int halfViewY = Constants.ViewportTilesY / 2 + Constants.ViewportMargin + 2;

        foreach (var entity in _world.GetEntitiesWith<PositionComponent>())
        {
            var pos = entity.GetComponent<PositionComponent>();

            // Frustum culling: solo interpolar entidades cercanas
            if (localPos != null &&
                (Math.Abs(pos.TileX - localPos.TileX) > halfViewX ||
                 Math.Abs(pos.TileY - localPos.TileY) > halfViewY))
            {
                // Snap directo para entidades lejanas (cuando vuelvan a ser visibles)
                pos.SnapToTarget();
                continue;
            }

            pos.Interpolate(deltaTime);
        }
    }
}
