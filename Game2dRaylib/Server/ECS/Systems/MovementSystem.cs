using Server.ECS.Components;
using Shared;

namespace Server.ECS.Systems;

public class MovementSystem : ISystem
{
    private readonly World _world;

    // Mapa simple: true = caminable, false = bloqueado
    private readonly bool[,] _walkableMap;

    public MovementSystem(World world)
    {
        _world = world;

        // Inicializar mapa: todo caminable excepto bordes
        _walkableMap = new bool[Constants.MapWidth, Constants.MapHeight];
        for (int x = 0; x < Constants.MapWidth; x++)
        for (int y = 0; y < Constants.MapHeight; y++)
            _walkableMap[x, y] = x > 0 && x < Constants.MapWidth - 1
                              && y > 0 && y < Constants.MapHeight - 1;
    }

    public void Update(float deltaTime)
    {
        foreach (var entity in _world.GetEntitiesWith<MovementQueueComponent>())
        {
            var pos   = entity.GetComponent<PositionComponent>();
            var speed = entity.GetComponent<SpeedComponent>();
            var queue = entity.GetComponent<MovementQueueComponent>();

            // Actualizar interpolación visual
            pos.UpdateVisual(deltaTime);

            // Si todavía está en movimiento, no procesar nuevo paso
            if (pos.IsMoving) continue;

            // Procesar dirección encolada
            if (queue.QueuedDirection.HasValue)
            {
                var dir = queue.QueuedDirection.Value;
                queue.QueuedDirection = null;
                queue.LastDirection   = dir;

                var (dx, dy) = DirectionHelper.ToOffset(dir);
                int newTileX = pos.TileX + dx;
                int newTileY = pos.TileY + dy;

                // Validar que el tile destino es caminable
                if (IsWalkable(newTileX, newTileY, entity))
                {
                    bool diagonal    = DirectionHelper.IsDiagonal(dir);
                    float stepDur    = Constants.StepDuration(speed.Speed, diagonal);
                    pos.StartMoveTo(newTileX, newTileY, stepDur);
                }
            }
        }
    }

    private bool IsWalkable(int tileX, int tileY, Entities.Entity movingEntity)
    {
        // Fuera de límites
        if (tileX < 0 || tileX >= Constants.MapWidth ||
            tileY < 0 || tileY >= Constants.MapHeight)
            return false;

        // Tile bloqueado en el mapa
        if (!_walkableMap[tileX, tileY])
            return false;

        // Verificar que no haya otro jugador en ese tile
        var netId = movingEntity.GetComponent<NetworkIdComponent>().Id;
        foreach (var other in _world.GetEntitiesWith<NetworkIdComponent>())
        {
            if (other.GetComponent<NetworkIdComponent>().Id == netId) continue;
            var otherPos = other.GetComponent<PositionComponent>();
            if (otherPos.TileX == tileX && otherPos.TileY == tileY)
                return false;
        }

        return true;
    }
}
