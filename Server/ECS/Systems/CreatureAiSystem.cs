using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Server.AI;
using Server.ECS;
using Server.ECS.Components;
using Server.Maps;
using Shared;

namespace Server.ECS.Systems;

/// <summary>
/// Per-tick FSM update for all creature entities.
/// Uses the optimized <see cref="AStarPathfinder"/> with <see cref="PathCache"/>
/// to avoid running A* every single tick.
///
/// State machine:
///   IDLE    -> ALERT   : player enters lookRange
///   ALERT   -> CHASE   : 0.5 s delay elapses
///   CHASE   -> ATTACK  : player within melee range (1 tile)
///   CHASE   -> RETURN  : player out of chaseRange or target gone
///   ATTACK  -> CHASE   : player moves out of melee range
///   ATTACK  -> FLEE    : HP below 15% and creature has Fleeing behavior
///   FLEE    -> RETURN  : target out of chaseRange
///   RETURN  -> IDLE    : reached spawn anchor
///   ANY     -> DEAD    : HP &lt;= 0
/// </summary>
public sealed class CreatureAiSystem : ISystem
{
    private readonly ServerWorld              _world;
    private readonly ILogger<CreatureAiSystem> _logger;
    private readonly PathCache                _pathCache = new();
    private CombatSystem?                 _combatSystem;

    /// <summary>Exposed so SpawnManager can invalidate on creature death.</summary>
    public PathCache PathCache => _pathCache;

    /// <summary>Wire in after DI to delegate attack damage to CombatSystem.</summary>
    public void SetCombatSystem(CombatSystem cs) => _combatSystem = cs;
    private MapData?                          _mapData;
    private readonly Random                   _rng = new();

    private const float AlertDelay    = 0.5f;
    private const int   MeleeRange    = 1;
    private const float FleeThreshold = 0.15f;
    private const float WanderMin     = 1.5f;
    private const float WanderMax     = 3.5f;
    private const float AttackBase    = 2.0f;

    private static readonly QueryDescription AiQuery = new QueryDescription()
        .WithAll<CreatureTag, CreatureComponent, CreatureAiComponent,
                 PositionComponent, MovementQueueComponent, SpeedComponent>();

    private static readonly QueryDescription PlayerQuery = new QueryDescription()
        .WithAll<PlayerTag, PositionComponent>();

    public CreatureAiSystem(ServerWorld world, ILogger<CreatureAiSystem> logger)
    {
        _world  = world;
        _logger = logger;
    }

    public void SetMapData(MapData map)
    {
        _mapData = map;
        _pathCache.Clear();
    }

    public void Update(float deltaTime)
    {
        _pathCache.Tick();

        _world.World.Query(in AiQuery,
            (Entity entity,
             ref CreatureComponent      creature,
             ref CreatureAiComponent    ai,
             ref PositionComponent      pos,
             ref MovementQueueComponent queue,
             ref SpeedComponent         speed) =>
        {
            if (ai.State == CreatureState.Dead) return;

            // ── Dead check ──────────────────────────────────────────────
            if (creature.CurrentHP <= 0)
            {
                ai.State = CreatureState.Dead;
                _pathCache.Invalidate(entity.Id);
                return;
            }

            // ── Resolve target ──────────────────────────────────────────
            bool hasTarget = ai.TargetEntity != Entity.Null
                          && ai.TargetEntity.IsAlive()
                          && ai.TargetEntity.Has<PositionComponent>();

            int targetX = 0, targetY = 0;
            if (hasTarget)
            {
                ref var tpos = ref ai.TargetEntity.Get<PositionComponent>();
                if (tpos.FloorZ != pos.FloorZ)
                    hasTarget = false;
                else
                {
                    targetX = tpos.TileX;
                    targetY = tpos.TileY;
                }
            }

            int dist = hasTarget
                ? ChebyshevDist(pos.TileX, pos.TileY, targetX, targetY)
                : int.MaxValue;

            if (ai.AttackCooldown > 0f) ai.AttackCooldown -= deltaTime;

            // ── FSM ──────────────────────────────────────────────────────
            switch (ai.State)
            {
                case CreatureState.Idle:
                    UpdateIdle(ref entity, ref creature, ref ai, ref pos, ref queue, deltaTime);
                    break;

                case CreatureState.Alert:
                    ai.StateTimer += deltaTime;
                    if (ai.StateTimer >= AlertDelay)
                    {
                        ai.State      = CreatureState.Chase;
                        ai.StateTimer = 0f;
                    }
                    break;

                case CreatureState.Chase:
                    if (!hasTarget || dist > creature.ChaseRange)
                    {
                        ai.State        = CreatureState.Return;
                        ai.TargetEntity = Entity.Null;
                        _pathCache.Invalidate(entity.Id);
                        break;
                    }
                    if (dist <= MeleeRange)
                    {
                        ai.State = CreatureState.Attack;
                        _pathCache.Invalidate(entity.Id);
                        break;
                    }
                    MoveToward(entity.Id, ref ai, ref pos, ref queue, targetX, targetY);
                    break;

                case CreatureState.Attack:
                    if (!hasTarget)
                    {
                        ai.State = CreatureState.Return;
                        break;
                    }
                    if (creature.Behavior == Shared.Creatures.CreatureBehavior.Fleeing
                        && (float)creature.CurrentHP / creature.MaxHP < FleeThreshold)
                    {
                        ai.State = CreatureState.Flee;
                        _pathCache.Invalidate(entity.Id);
                        break;
                    }
                    if (dist > MeleeRange)
                    {
                        ai.State = CreatureState.Chase;
                        break;
                    }
                    if (ai.AttackCooldown <= 0f)
                    {
                        ExecuteAttack(ref creature, ref ai);
                        ai.AttackCooldown = AttackCooldownFor(speed.Speed);
                    }
                    break;

                case CreatureState.Flee:
                    if (!hasTarget || dist > creature.ChaseRange)
                    {
                        ai.State = CreatureState.Return;
                        _pathCache.Invalidate(entity.Id);
                        break;
                    }
                    FleeFrom(ref ai, ref pos, ref queue, targetX, targetY);
                    break;

                case CreatureState.Return:
                    int distToSpawn = ChebyshevDist(pos.TileX, pos.TileY, ai.SpawnX, ai.SpawnY);
                    if (distToSpawn <= 1)
                    {
                        ai.State = CreatureState.Idle;
                        _pathCache.Invalidate(entity.Id);
                        break;
                    }
                    MoveToward(entity.Id, ref ai, ref pos, ref queue, ai.SpawnX, ai.SpawnY);
                    break;
            }

            // ── Passive aggro scan (IDLE only) ───────────────────────────
            if (ai.State == CreatureState.Idle && creature.IsAggressive)
            {
                var nearest = FindNearestPlayer(pos.TileX, pos.TileY, pos.FloorZ, creature.LookRange);
                if (nearest != Entity.Null)
                {
                    ai.TargetEntity = nearest;
                    ai.State        = CreatureState.Alert;
                    ai.StateTimer   = 0f;
                }
            }

            // ── Re-acquire target if lost in Chase/Attack ────────────────
            if ((ai.State == CreatureState.Chase || ai.State == CreatureState.Attack)
                && !hasTarget && creature.IsAggressive)
            {
                var nearest = FindNearestPlayer(pos.TileX, pos.TileY, pos.FloorZ, creature.ChaseRange);
                if (nearest != Entity.Null)
                    ai.TargetEntity = nearest;
                else
                {
                    ai.State = CreatureState.Return;
                    _pathCache.Invalidate(entity.Id);
                }
            }
        });
    }

    // ── Movement helpers ──────────────────────────────────────────────────────

    private void UpdateIdle(
        ref Entity entity,
        ref CreatureComponent creature,
        ref CreatureAiComponent ai,
        ref PositionComponent pos,
        ref MovementQueueComponent queue,
        float deltaTime)
    {
        ai.WanderCooldown -= deltaTime;
        if (ai.WanderCooldown > 0f) return;

        ai.WanderCooldown = WanderMin + (float)(_rng.NextDouble() * (WanderMax - WanderMin));
        if (_rng.NextDouble() > 0.30) return;

        Direction[] dirs = { Direction.North, Direction.South, Direction.East, Direction.West };
        var dir = dirs[_rng.Next(dirs.Length)];
        var (dx, dy) = DirectionHelper.ToOffset(dir);
        int nx = pos.TileX + dx;
        int ny = pos.TileY + dy;

        if (ChebyshevDist(nx, ny, ai.SpawnX, ai.SpawnY) > 3) return;
        if (_mapData != null && !_mapData.Walkable[nx, ny, pos.FloorZ]) return;

        queue.QueuedDirection = (byte)dir;
    }

    private void MoveToward(
        int entityId,
        ref CreatureAiComponent ai,
        ref PositionComponent pos,
        ref MovementQueueComponent queue,
        int goalX, int goalY)
    {
        if (pos.IsMoving || _mapData == null) return;

        // Try cached path first
        if (_pathCache.TryConsume(entityId, goalX, goalY, out int dx, out int dy))
        {
            if (dx != 0 || dy != 0)
                queue.QueuedDirection = (byte)DirectionHelper.FromOffset(dx, dy);
            return;
        }

        // Compute new path and cache it
        var path = AStarPathfinder.FindPath(
            pos.TileX, pos.TileY, pos.FloorZ,
            goalX, goalY, _mapData);

        var (fdx, fdy) = _pathCache.Store(entityId, goalX, goalY, path);
        if (fdx != 0 || fdy != 0)
            queue.QueuedDirection = (byte)DirectionHelper.FromOffset(fdx, fdy);
    }

    private void FleeFrom(
        ref CreatureAiComponent ai,
        ref PositionComponent pos,
        ref MovementQueueComponent queue,
        int threatX, int threatY)
    {
        if (pos.IsMoving) return;

        int dx = pos.TileX - threatX;
        int dy = pos.TileY - threatY;
        dx = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
        dy = dy == 0 ? 0 : (dy > 0 ? 1 : -1);
        if (dx == 0 && dy == 0) dx = 1;

        int nx = pos.TileX + dx;
        int ny = pos.TileY + dy;
        if (_mapData != null && !_mapData.Walkable[nx, ny, pos.FloorZ]) return;

        queue.QueuedDirection = (byte)DirectionHelper.FromOffset(dx, dy);
    }

    private void ExecuteAttack(ref CreatureComponent creature, ref CreatureAiComponent ai)
    {
        if (!ai.TargetEntity.IsAlive()) return;

        if (_combatSystem != null)
        {
            _combatSystem.ApplyCreatureAttack(ref creature, ai.TargetEntity);
        }
        else
        {
            // Fallback if CombatSystem not yet wired (e.g. during tests)
            if (!ai.TargetEntity.Has<StatsComponent>()) return;
            int dmg = _rng.Next(creature.AttackMin, creature.AttackMax + 1);
            ref var targetStats = ref ai.TargetEntity.Get<StatsComponent>();
            targetStats.CurrentHP = Math.Max(0, targetStats.CurrentHP - dmg);
        }
    }

    // ── Spatial helpers ───────────────────────────────────────────────────────

    private Entity FindNearestPlayer(int fromX, int fromY, byte floor, int maxRange)
    {
        Entity nearest  = Entity.Null;
        int    bestDist = maxRange + 1;

        _world.World.Query(in PlayerQuery,
            (Entity e, ref PositionComponent ppos) =>
        {
            if (ppos.FloorZ != floor) return;
            int d = ChebyshevDist(fromX, fromY, ppos.TileX, ppos.TileY);
            if (d <= maxRange && d < bestDist) { bestDist = d; nearest = e; }
        });

        return nearest;
    }

    private static int ChebyshevDist(int x1, int y1, int x2, int y2)
        => Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));

    private static float AttackCooldownFor(float speed)
    {
        float s = Math.Clamp(speed, 100f, 400f);
        return AttackBase * (1f - (s - 100f) / 300f * 0.7f);
    }
}
