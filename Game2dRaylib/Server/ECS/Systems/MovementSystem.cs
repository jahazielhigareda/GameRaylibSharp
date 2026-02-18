using Arch.Core;
using Server.ECS;
using Server.ECS.Components;
using Shared;

namespace Server.ECS.Systems;

/// <summary>
/// Arch-based movement system.
/// Processes Tibia-style queued movement for all movable entities.
/// </summary>
public class MovementSystem : ISystem
{
    private readonly ServerWorld _world;
    private readonly bool[,]    _walkableMap;

    // Cached query (players + creatures that can move)
    private static readonly QueryDescription MovableQuery = new QueryDescription()
        .WithAll<MovementQueueComponent, PositionComponent, SpeedComponent>();

    private static readonly QueryDescription OccupancyQuery = new QueryDescription()
        .WithAll<PositionComponent>()
        .WithAny<PlayerTag, CreatureTag>();

    public MovementSystem(ServerWorld world)
    {
        _world       = world;
        _walkableMap = new bool[Constants.MapWidth, Constants.MapHeight];
        for (int x = 0; x < Constants.MapWidth; x++)
        for (int y = 0; y < Constants.MapHeight; y++)
            _walkableMap[x, y] = x > 0 && x < Constants.MapWidth - 1
                              && y > 0 && y < Constants.MapHeight - 1;
    }

    public void Update(float deltaTime)
    {
        _world.World.Query(in MovableQuery,
            (Entity entity,
             ref MovementQueueComponent queue,
             ref PositionComponent pos,
             ref SpeedComponent speed) =>
        {
            pos.UpdateVisual(deltaTime);

            if (pos.IsMoving || !queue.HasQueued) return;

            var dir = (Direction)queue.QueuedDirection;
            queue.QueuedDirection = (byte)Direction.None;
            queue.LastDirection   = (byte)dir;

            var (dx, dy) = DirectionHelper.ToOffset(dir);
            int newX = pos.TileX + dx;
            int newY = pos.TileY + dy;

            if (IsWalkable(newX, newY, entity))
            {
                bool diagonal = DirectionHelper.IsDiagonal(dir);
                float stepDur = Constants.StepDuration(speed.Speed, diagonal);
                pos.StartMoveTo(newX, newY, stepDur);
            }
        });
    }

    private bool IsWalkable(int tileX, int tileY, Entity mover)
    {
        if (tileX < 0 || tileX >= Constants.MapWidth ||
            tileY < 0 || tileY >= Constants.MapHeight)
            return false;

        if (!_walkableMap[tileX, tileY]) return false;

        // Check tile occupation by other solid entities
        bool blocked = false;
        _world.World.Query(in OccupancyQuery, (Entity other, ref PositionComponent otherPos) =>
        {
            if (other == mover) return;
            if (otherPos.TileX == tileX && otherPos.TileY == tileY) blocked = true;
        });

        return !blocked;
    }
}
