using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Server.AI;
using Server.Combat;
using Server.ECS;
using Server.ECS.Components;
using Server.Maps;
using Shared;

namespace Server.ECS.Systems;

/// <summary>
/// Per-tick creature FSM — Tibia-accurate chasing.
///
/// Tibia creature movement rules (TFS reference):
///   1. A* is computed every tick toward an ADJACENT goal tile.
///      The path ignores creature/player occupancy — only walls matter.
///   2. QueuedDirection is set every tick. MovementSystem consumes it
///      only when the previous step finishes (pos.IsMoving == false).
///      This means the creature "pre-queues" the next step while still moving.
///   3. If A* returns (0,0) and creature is not yet adjacent, it attempts
///      a random cardinal step to escape deadlock (TFS equivalent of
///      "pushCreatures" / randomStep logic).
///   4. Creatures DO block each other at the MovementSystem level.
///      A* ignores this — the creature retries automatically next tick.
///
/// FSM states:
///   IDLE   → ALERT  : player within LookRange + LoS
///   ALERT  → CHASE  : 0.5 s delay
///   CHASE  → ATTACK : Chebyshev dist ≤ 1 (adjacent)
///   ATTACK → CHASE  : player moves away
///   CHASE  → RETURN : player out of ChaseRange or target lost
///   RETURN → IDLE   : reached spawn tile (within 1 tile)
///   ANY    → DEAD   : HP ≤ 0
/// </summary>
public sealed class CreatureAiSystem : ISystem
{
    private readonly ServerWorld               _world;
    private readonly ILogger<CreatureAiSystem> _logger;
    private readonly PathCache                 _pathCache = new();
    private CombatSystem?                      _combatSystem;
    private MapData?                           _mapData;
    private readonly Random                    _rng = new();

    private const float AlertDelay    = 0.5f;
    private const int   MeleeRange    = 1;
    private const float FleeThreshold = 0.15f;
    private const float WanderMin     = 2.0f;
    private const float WanderMax     = 4.5f;

    private static readonly QueryDescription AiQuery = new QueryDescription()
        .WithAll<CreatureTag, CreatureComponent, CreatureAiComponent,
                 PositionComponent, MovementQueueComponent, SpeedComponent>();

    private static readonly QueryDescription PlayerQuery = new QueryDescription()
        .WithAll<PlayerTag, PositionComponent>();

    public PathCache PathCache => _pathCache;

    public void SetCombatSystem(CombatSystem cs) => _combatSystem = cs;

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

    // ── Main update ───────────────────────────────────────────────────────

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

            if (creature.CurrentHP <= 0)
            {
                ai.State = CreatureState.Dead;
                _pathCache.Invalidate(entity.Id);
                return;
            }

            // ── Resolve target position ───────────────────────────────────
            bool hasTarget = ai.TargetEntity != Entity.Null
                          && ai.TargetEntity.IsAlive()
                          && ai.TargetEntity.Has<PositionComponent>();

            int targetX = 0, targetY = 0;
            if (hasTarget)
            {
                ref var tpos = ref ai.TargetEntity.Get<PositionComponent>();
                if (tpos.FloorZ != pos.FloorZ)
                    hasTarget = false;
                else { targetX = tpos.TileX; targetY = tpos.TileY; }
            }

            int dist = hasTarget ? Chebyshev(pos.TileX, pos.TileY, targetX, targetY) : int.MaxValue;

            if (ai.AttackCooldown > 0f) ai.AttackCooldown -= deltaTime;

            // ── FSM ───────────────────────────────────────────────────────
            switch (ai.State)
            {
                // ── IDLE ──────────────────────────────────────────────────
                case CreatureState.Idle:
                    UpdateIdle(ref creature, ref ai, ref pos, ref queue, deltaTime);
                    break;

                // ── ALERT (0.5 s aggro delay) ─────────────────────────────
                case CreatureState.Alert:
                    ai.StateTimer += deltaTime;
                    if (ai.StateTimer >= AlertDelay)
                    {
                        ai.State      = CreatureState.Chase;
                        ai.StateTimer = 0f;
                    }
                    break;

                // ── CHASE ─────────────────────────────────────────────────
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
                    // Compute and queue movement every tick
                    ChaseTarget(entity.Id, ref ai, ref pos, ref queue, targetX, targetY);
                    break;

                // ── ATTACK ────────────────────────────────────────────────
                case CreatureState.Attack:
                    if (!hasTarget)
                    {
                        ai.State = CreatureState.Return;
                        _pathCache.Invalidate(entity.Id);
                        break;
                    }
                    if (dist > MeleeRange)
                    {
                        // Player moved — go back to chasing immediately
                        ai.State = CreatureState.Chase;
                        break;
                    }
                    if (creature.Behavior == Shared.Creatures.CreatureBehavior.Fleeing
                        && (float)creature.CurrentHP / creature.MaxHP < FleeThreshold)
                    {
                        ai.State = CreatureState.Flee;
                        _pathCache.Invalidate(entity.Id);
                        break;
                    }
                    if (ai.AttackCooldown <= 0f)
                    {
                        ExecuteAttack(ref creature, ref ai);
                        ai.AttackCooldown = AttackCooldownFor(speed.Speed);
                    }
                    break;

                // ── FLEE ──────────────────────────────────────────────────
                case CreatureState.Flee:
                    if (!hasTarget || dist > creature.ChaseRange)
                    {
                        ai.State = CreatureState.Return;
                        _pathCache.Invalidate(entity.Id);
                        break;
                    }
                    FleeFrom(ref ai, ref pos, ref queue, targetX, targetY);
                    break;

                // ── RETURN ────────────────────────────────────────────────
                case CreatureState.Return:
                    int distToSpawn = Chebyshev(pos.TileX, pos.TileY, ai.SpawnX, ai.SpawnY);
                    if (distToSpawn <= 1)
                    {
                        ai.State = CreatureState.Idle;
                        _pathCache.Invalidate(entity.Id);
                        break;
                    }
                    ReturnToSpawn(entity.Id, ref ai, ref pos, ref queue);
                    break;
            }

            // ── Passive aggro scan (IDLE only) ───────────────────────────
            if (ai.State == CreatureState.Idle && creature.IsAggressive)
            {
                var nearest = FindNearestPlayerWithLoS(
                    pos.TileX, pos.TileY, pos.FloorZ, creature.LookRange);
                if (nearest != Entity.Null)
                {
                    ai.TargetEntity = nearest;
                    ai.State        = CreatureState.Alert;
                    ai.StateTimer   = 0f;
                }
            }

            // ── Re-acquire lost target ───────────────────────────────────
            if ((ai.State == CreatureState.Chase || ai.State == CreatureState.Attack)
                && !hasTarget && creature.IsAggressive)
            {
                var nearest = FindNearestPlayerWithLoS(
                    pos.TileX, pos.TileY, pos.FloorZ, creature.ChaseRange);
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

    // ── Movement helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Tibia-accurate chase: A* every tick, target = adjacent tile to player.
    /// QueuedDirection set regardless of pos.IsMoving so MovementSystem can
    /// consume it immediately when the current step finishes.
    /// </summary>
    private void ChaseTarget(
        int entityId,
        ref CreatureAiComponent    ai,
        ref PositionComponent      pos,
        ref MovementQueueComponent queue,
        int targetX, int targetY)
    {
        if (_mapData == null) return;

        // Compute next step every tick — no stale cache for chase
        var (dx, dy) = AStarPathfinder.NextStep(
            pos.TileX, pos.TileY, pos.FloorZ,
            targetX, targetY, _mapData);

        if (dx != 0 || dy != 0)
        {
            queue.QueuedDirection = (byte)DirectionHelper.FromOffset(dx, dy);
            return;
        }

        // A* returned (0,0) but we are not adjacent — STUCK behind a creature.
        // Try a random cardinal step to escape (TFS pushCreatures equivalent).
        if (Chebyshev(pos.TileX, pos.TileY, targetX, targetY) > MeleeRange
            && !pos.IsMoving)
        {
            TryRandomCardinalStep(ref pos, ref queue);
        }
    }

    private void UpdateIdle(
        ref CreatureComponent      creature,
        ref CreatureAiComponent    ai,
        ref PositionComponent      pos,
        ref MovementQueueComponent queue,
        float deltaTime)
    {
        ai.WanderCooldown -= deltaTime;
        if (ai.WanderCooldown > 0f) return;

        ai.WanderCooldown = WanderMin + (float)(_rng.NextDouble() * (WanderMax - WanderMin));
        if (_rng.NextDouble() > 0.4) return;

        Direction[] dirs = { Direction.North, Direction.South, Direction.East, Direction.West };
        var dir = dirs[_rng.Next(dirs.Length)];
        var (ddx, ddy) = DirectionHelper.ToOffset(dir);
        int nx = pos.TileX + ddx;
        int ny = pos.TileY + ddy;

        if (Chebyshev(nx, ny, ai.SpawnX, ai.SpawnY) > 3) return;
        if (_mapData != null && !_mapData.Walkable[nx, ny, pos.FloorZ]) return;

        queue.QueuedDirection = (byte)dir;
    }

    private void ReturnToSpawn(
        int entityId,
        ref CreatureAiComponent    ai,
        ref PositionComponent      pos,
        ref MovementQueueComponent queue)
    {
        if (pos.IsMoving || _mapData == null) return;

        // PathCache is fine for return — spawn is a static goal
        if (_pathCache.TryConsume(entityId, ai.SpawnX, ai.SpawnY, out int dx, out int dy))
        {
            if (dx != 0 || dy != 0)
                queue.QueuedDirection = (byte)DirectionHelper.FromOffset(dx, dy);
            return;
        }

        var path = AStarPathfinder.FindPath(
            pos.TileX, pos.TileY, pos.FloorZ,
            ai.SpawnX, ai.SpawnY, _mapData);

        var (fdx, fdy) = _pathCache.Store(entityId, ai.SpawnX, ai.SpawnY, path);
        if (fdx != 0 || fdy != 0)
            queue.QueuedDirection = (byte)DirectionHelper.FromOffset(fdx, fdy);
    }

    private void FleeFrom(
        ref CreatureAiComponent    ai,
        ref PositionComponent      pos,
        ref MovementQueueComponent queue,
        int threatX, int threatY)
    {
        if (pos.IsMoving) return;

        int dx = pos.TileX - threatX;
        int dy = pos.TileY - threatY;
        dx = dx == 0 ? (_rng.NextDouble() > 0.5 ? 1 : -1) : (dx > 0 ? 1 : -1);
        dy = dy == 0 ? (_rng.NextDouble() > 0.5 ? 1 : -1) : (dy > 0 ? 1 : -1);

        int nx = pos.TileX + dx;
        int ny = pos.TileY + dy;
        if (_mapData != null && !_mapData.Walkable[nx, ny, pos.FloorZ]) return;

        queue.QueuedDirection = (byte)DirectionHelper.FromOffset(dx, dy);
    }

    /// <summary>Random cardinal step to escape deadlock.</summary>
    private void TryRandomCardinalStep(
        ref PositionComponent      pos,
        ref MovementQueueComponent queue)
    {
        if (_mapData == null) return;

        Direction[] cards = { Direction.North, Direction.South, Direction.East, Direction.West };
        // Fisher-Yates shuffle
        for (int i = cards.Length - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }

        foreach (var dir in cards)
        {
            var (ddx, ddy) = DirectionHelper.ToOffset(dir);
            int nx = pos.TileX + ddx;
            int ny = pos.TileY + ddy;
            if (nx < 0 || nx >= _mapData.Width || ny < 0 || ny >= _mapData.Height) continue;
            if (!_mapData.Walkable[nx, ny, pos.FloorZ]) continue;
            queue.QueuedDirection = (byte)dir;
            return;
        }
    }

    private void ExecuteAttack(ref CreatureComponent creature, ref CreatureAiComponent ai)
    {
        if (!ai.TargetEntity.IsAlive()) return;
        if (_combatSystem != null)
            _combatSystem.ApplyCreatureAttack(ref creature, ai.TargetEntity);
        else
            _logger.LogWarning("CombatSystem not wired — creature attack skipped.");
    }

    private static float AttackCooldownFor(float speed)
    {
        float factor = Math.Clamp(speed / 100f, 0.5f, 2.0f);
        return 2.0f / factor;
    }

    private static int Chebyshev(int ax, int ay, int bx, int by)
        => Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

    // ── Scan helpers ──────────────────────────────────────────────────────

    private Entity FindNearestPlayerWithLoS(int cx, int cy, byte floor, int range)
    {
        Entity nearest  = Entity.Null;
        int    bestDist = int.MaxValue;

        _world.World.Query(in PlayerQuery,
            (Entity e, ref PositionComponent ppos) =>
            {
                if (ppos.FloorZ != floor) return;
                int d = Chebyshev(cx, cy, ppos.TileX, ppos.TileY);
                if (d > range || d >= bestDist) return;

                if (_mapData != null
                    && !LosChecker.HasLineOfSight(_mapData, cx, cy, ppos.TileX, ppos.TileY, floor))
                    return;

                bestDist = d;
                nearest  = e;
            });

        return nearest;
    }
}
