using Server.Maps;
using Server.Events;
using Server.Network;
using Arch.Core;
using Arch.Core.Extensions;
using Server.ECS;
using Server.ECS.Components;
using Shared;
using Shared.Packets;

namespace Server.ECS.Systems;

/// <summary>
/// Arch-based movement system.
/// Processes Tibia-style queued movement for all movable entities.
/// Supports multi-floor movement via StairUp / StairDown / RopeSpot tiles.
/// </summary>
public class MovementSystem : ISystem
{
    private readonly ServerWorld    _world;
    private readonly EventBus       _eventBus;
    private MapData?                _mapData;

    // Fallback 2-D walkable map used when no MapData is loaded
    private bool[,] _walkableMap;

    private static readonly QueryDescription MovableQuery = new QueryDescription()
        .WithAll<MovementQueueComponent, PositionComponent, SpeedComponent>();

    private static readonly QueryDescription OccupancyQuery = new QueryDescription()
        .WithAll<PositionComponent>()
        .WithAny<PlayerTag, CreatureTag>();

    // Injected lazily (NetworkManager registered after MovementSystem in DI)
    public NetworkManager? NetworkManager { get; set; }

    public MovementSystem(ServerWorld world, EventBus eventBus)
    {
        _world       = world;
        _eventBus    = eventBus;
        _walkableMap = BuildDefaultWalkableMap();
    }

    /// <summary>Replaces the default walkable map with the loaded MapData.</summary>
    public void SetMapData(MapData map)
    {
        _mapData     = map;
        int w = map.Width;
        int h = map.Height;
        _walkableMap = new bool[w, h];
        // ground floor (z=0) 2-D fallback
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
            byte newZ = pos.FloorZ;

            // ── Check for floor-change tiles at the *destination* position ──
            if (_mapData != null)
            {
                // First resolve the 2-D move (same floor), then check tile type
                if (IsWalkable3D(newX, newY, newZ, entity))
                {
                    var destTile = _mapData.Tiles[newX, newY, newZ];
                    var destType = TileTypeHelper.FromGroundId(destTile.GroundItemId);

                    int finalX = newX, finalY = newY;
                    byte finalZ = newZ;
                    bool floorChanged = false;

                    switch (destType)
                    {
                        case TileType.StairUp when newZ > 0:
                            if (IsWalkable3D(newX, newY, (byte)(newZ - 1), entity))
                            {
                                finalZ      = (byte)(newZ - 1);
                                floorChanged = true;
                            }
                            break;

                        case TileType.StairDown when newZ + 1 < _mapData.Floors:
                            if (IsWalkable3D(newX, newY, (byte)(newZ + 1), entity))
                            {
                                finalZ      = (byte)(newZ + 1);
                                floorChanged = true;
                            }
                            break;

                        case TileType.RopeSpot when newZ > 0:
                            if (IsWalkable3D(newX, newY, (byte)(newZ - 1), entity))
                            {
                                finalZ      = (byte)(newZ - 1);
                                floorChanged = true;
                            }
                            break;
                    }

                    int fromX = pos.TileX, fromY = pos.TileY;
                    byte oldZ = pos.FloorZ;
                    bool diagonal = DirectionHelper.IsDiagonal(dir);
                    float stepDur = Constants.StepDuration(speed.Speed, diagonal);

                    pos.StartMoveTo(finalX, finalY, stepDur);
                    pos.FloorZ = finalZ;

                    // Notify event bus
                    int eid = entity.Has<NetworkIdComponent>()
                        ? entity.Get<NetworkIdComponent>().Id
                        : entity.Id;

                    _eventBus.Publish(new CreatureMoved
                    {
                        EntityId = eid,
                        FromX    = fromX,
                        FromY    = fromY,
                        ToX      = finalX,
                        ToY      = finalY
                    });

                    // Send FloorChangePacket to the player if floor changed
                    if (floorChanged && entity.Has<PlayerTag>() && NetworkManager != null)
                    {
                        var pkt = new FloorChangePacket
                        {
                            FromZ = oldZ,
                            ToZ   = finalZ,
                            X     = finalX,
                            Y     = finalY
                        };
                        NetworkManager.SendFloorChange(eid, pkt);
                    }
                }
            }
            else
            {
                // Legacy 2-D path (no map loaded)
                if (IsWalkable2D(newX, newY, entity))
                {
                    int fromX = pos.TileX, fromY = pos.TileY;
                    bool diagonal = DirectionHelper.IsDiagonal(dir);
                    float stepDur = Constants.StepDuration(speed.Speed, diagonal);
                    pos.StartMoveTo(newX, newY, stepDur);

                    int eid = entity.Has<NetworkIdComponent>()
                        ? entity.Get<NetworkIdComponent>().Id
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
            }
        });
    }

    // ── Walkability helpers ───────────────────────────────────────────────────

    private bool IsWalkable3D(int tileX, int tileY, byte z, Entity mover)
    {
        if (_mapData == null) return IsWalkable2D(tileX, tileY, mover);

        if (tileX < 0 || tileX >= _mapData.Width  ||
            tileY < 0 || tileY >= _mapData.Height  ||
            z < 0     || z >= _mapData.Floors)
            return false;

        if (!_mapData.Walkable[tileX, tileY, z]) return false;

        bool blocked = false;
        _world.World.Query(in OccupancyQuery, (Entity other, ref PositionComponent otherPos) =>
        {
            if (other == mover) return;
            if (otherPos.TileX == tileX && otherPos.TileY == tileY && otherPos.FloorZ == z)
                blocked = true;
        });
        return !blocked;
    }

    private bool IsWalkable2D(int tileX, int tileY, Entity mover)
    {
        int mapW = _walkableMap.GetLength(0);
        int mapH = _walkableMap.GetLength(1);
        if (tileX < 0 || tileX >= mapW || tileY < 0 || tileY >= mapH)
            return false;
        if (!_walkableMap[tileX, tileY]) return false;

        bool blocked = false;
        _world.World.Query(in OccupancyQuery, (Entity other, ref PositionComponent otherPos) =>
        {
            if (other == mover) return;
            if (otherPos.TileX == tileX && otherPos.TileY == tileY) blocked = true;
        });
        return !blocked;
    }
}
