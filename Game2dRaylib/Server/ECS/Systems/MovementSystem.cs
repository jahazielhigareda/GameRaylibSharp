using Server.Maps;
using Server.Events;
using Arch.Core;
using Arch.Core.Extensions;
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
    private bool[,] _walkableMap;

    // Cached query (players + creatures that can move)
    private static readonly QueryDescription MovableQuery = new QueryDescription()
        .WithAll<MovementQueueComponent, PositionComponent, SpeedComponent>();

    private static readonly QueryDescription OccupancyQuery = new QueryDescription()
        .WithAll<PositionComponent>()
        .WithAny<PlayerTag, CreatureTag>();

    private readonly EventBus _eventBus;

    public MovementSystem(ServerWorld world, EventBus eventBus)
    {
        _world       = world;
        _eventBus    = eventBus;
        _walkableMap = BuildDefaultWalkableMap();
    }

    /// <summary>Replaces the default walkable map with the loaded MapData.</summary>
    public void SetMapData(MapData map)
    {
        int w = map.Width;
        int h = map.Height;
        _walkableMap = new bool[w, h];
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            _walkableMap[x, y] = map.Walkable[x, y, 0];
    }

    private static bool[,] BuildDefaultWalkableMap()
    {
        var m = new bool[Constants.MapWidth, Constants.MapHeight];
        for (int x = 0; x < Constants.MapWidth; x++)
        for (int y = 0; y < Constants.MapHeight; y++)
            m[x, y] = x > 0 && x < Constants.MapWidth  - 1
                   && y > 0 && y < Constants.MapHeight - 1;
        return m;
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
                int fromX = pos.TileX;
                int fromY = pos.TileY;
                bool diagonal = DirectionHelper.IsDiagonal(dir);
                float stepDur = Constants.StepDuration(speed.Speed, diagonal);
                pos.StartMoveTo(newX, newY, stepDur);

                int eid = entity.Has<Server.ECS.Components.NetworkIdComponent>()
                    ? entity.Get<Server.ECS.Components.NetworkIdComponent>().Id
                    : entity.Id;

                _eventBus.Publish(new CreatureMoved
                {
                    EntityId = eid,
                    FromX    = fromX,
                    FromY    = fromY,
                    ToX      = newX,
                    ToY      = newY
                });
            }
        });
    }

    private bool IsWalkable(int tileX, int tileY, Entity mover)
    {
        int mapW = _walkableMap.GetLength(0);
        int mapH = _walkableMap.GetLength(1);
        if (tileX < 0 || tileX >= mapW ||
            tileY < 0 || tileY >= mapH)
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
