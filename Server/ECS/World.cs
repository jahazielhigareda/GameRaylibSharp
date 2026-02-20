using Arch.Core;
using Arch.Core.Extensions;
using Server.ECS.Components;
using Shared;

namespace Server.ECS;

/// <summary>
/// Thin façade around Arch.Core.World.
/// Keeps the same surface API as before so callers need minimal changes.
/// </summary>
public sealed class ServerWorld : IDisposable
{
    public readonly Arch.Core.World World;

    public ServerWorld() { World = Arch.Core.World.Create(); }

    // ── Archetype queries (cached) ────────────────────────────────────────

    private static readonly QueryDescription PlayerQuery = new QueryDescription()
        .WithAll<PlayerTag, NetworkIdComponent, PositionComponent,
                 SpeedComponent, MovementQueueComponent, StatsComponent, SkillsComponent>();

    private static readonly QueryDescription CreatureQuery = new QueryDescription()
        .WithAll<CreatureTag, PositionComponent, CreatureComponent>();

    private static readonly QueryDescription NpcQuery = new QueryDescription()
        .WithAll<NpcTag, PositionComponent, CreatureComponent>();

    private static readonly QueryDescription ItemQuery = new QueryDescription()
        .WithAll<ItemTag, PositionComponent, ItemComponent>();

    private static readonly QueryDescription ProjectileQuery = new QueryDescription()
        .WithAll<ProjectileTag, PositionComponent, ProjectileComponent>();

    private static readonly QueryDescription MovableQuery = new QueryDescription()
        .WithAll<MovementQueueComponent, PositionComponent, SpeedComponent>();

    private static readonly QueryDescription StatsQuery = new QueryDescription()
        .WithAll<StatsComponent>();

    private static readonly QueryDescription NetworkedQuery = new QueryDescription()
        .WithAll<NetworkIdComponent, PositionComponent>();

    // ── Entity creation helpers ───────────────────────────────────────────

    /// <summary>Spawn a player archetype.</summary>
    public Entity SpawnPlayer(int networkId, int tileX, int tileY,
                              Vocation vocation = Vocation.None,
                              byte floorZ = 7)
    {
        var pos = new PositionComponent();
        pos.SetTilePosition(tileX, tileY);
        pos.FloorZ = floorZ;

        var stats = new StatsComponent();
        stats.Initialize(vocation);

        return World.Create(
            new PlayerTag(),
            new NetworkIdComponent { Id = networkId },
            pos,
            new SpeedComponent(),
            new MovementQueueComponent(),
            stats,
            new SkillsComponent(),
            new CombatComponent());
    }

    /// <summary>Spawn a creature archetype.</summary>
    public Entity SpawnCreature(ushort creatureId, int tileX, int tileY,
                                int maxHp, bool aggressive = true,
                                byte floorZ = 7)
    {
        var pos = new PositionComponent();
        pos.SetTilePosition(tileX, tileY);
        pos.FloorZ = floorZ;

        return World.Create(
            new CreatureTag(),
            pos,
            new SpeedComponent(),
            new MovementQueueComponent(),
            new CreatureComponent
            {
                CreatureId   = creatureId,
                MaxHP        = maxHp,
                CurrentHP    = maxHp,
                IsAggressive = aggressive
            },
            new CreatureAiComponent
            {
                State    = CreatureState.Idle,
                SpawnX   = tileX,
                SpawnY   = tileY,
                SpawnFloor = floorZ,
                PathTargetX = -1,
                PathTargetY = -1,
            });
    }


    /// <summary>Spawn a creature from a <see cref="Shared.Creatures.CreatureTemplate"/>.</summary>
    public Entity SpawnCreature(Shared.Creatures.CreatureTemplate template,
                                int tileX, int tileY, byte floorZ = 7)
    {
        var pos = new PositionComponent();
        pos.SetTilePosition(tileX, tileY);
        pos.FloorZ = floorZ;

        return World.Create(
            new CreatureTag(),
            pos,
            new SpeedComponent(),
            new MovementQueueComponent(),
            new CreatureComponent
            {
                CreatureId   = template.Id,
                MaxHP        = template.MaxHP,
                CurrentHP    = template.MaxHP,
                MaxMP        = template.MaxMP,
                CurrentMP    = template.MaxMP,
                Experience   = template.Experience,
                AttackMin    = template.AttackMin,
                AttackMax    = template.AttackMax,
                Armor        = template.Armor,
                Defense      = template.Defense,
                LookRange    = template.LookRange,
                ChaseRange   = template.ChaseRange,
                Behavior     = template.Behavior,
                IsAggressive = template.Behavior != Shared.Creatures.CreatureBehavior.Passive,
            },
            new CreatureAiComponent
            {
                State      = CreatureState.Idle,
                SpawnX     = tileX,
                SpawnY     = tileY,
                SpawnFloor = floorZ,
                PathTargetX = -1,
                PathTargetY = -1,
            });
    }

    /// <summary>Spawn an NPC archetype.</summary>
    public Entity SpawnNpc(ushort creatureId, int tileX, int tileY, int maxHp,
                           byte floorZ = 7)
    {
        var pos = new PositionComponent();
        pos.SetTilePosition(tileX, tileY);
        pos.FloorZ = floorZ;

        return World.Create(
            new NpcTag(),
            pos,
            new CreatureComponent
            {
                CreatureId = creatureId,
                MaxHP      = maxHp,
                CurrentHP  = maxHp
            });
    }

    /// <summary>Spawn a ground item archetype.</summary>
    public Entity SpawnItem(ushort itemId, int tileX, int tileY,
                            byte count = 1, bool pickable = true)
    {
        var pos = new PositionComponent();
        pos.SetTilePosition(tileX, tileY);

        return World.Create(
            new ItemTag(),
            pos,
            new ItemComponent { ItemId = itemId, Count = count, IsPickable = pickable });
    }

    /// <summary>Spawn a projectile archetype.</summary>
    public Entity SpawnProjectile(int ownerNetId, byte type,
                                  int tileX, int tileY,
                                  int targetTileX, int targetTileY,
                                  float speed = 300f, float lifeTime = 2f)
    {
        var pos = new PositionComponent();
        pos.SetTilePosition(tileX, tileY);

        return World.Create(
            new ProjectileTag(),
            pos,
            new ProjectileComponent
            {
                OwnerNetId     = ownerNetId,
                ProjectileType = type,
                TargetTileX    = targetTileX,
                TargetTileY    = targetTileY,
                Speed          = speed,
                LifeTime       = lifeTime
            });
    }

    public void DestroyEntity(Entity entity) => World.Destroy(entity);

    // ── Query helpers (matching old API) ─────────────────────────────────

    /// <summary>Find player entity by network id. Returns Entity.Null if not found.</summary>
    public Entity FindPlayer(int networkId)
    {
        Entity found = Entity.Null;
        World.Query(in PlayerQuery, (Entity e, ref NetworkIdComponent nid) =>
        {
            if (nid.Id == networkId) found = e;
        });
        return found;
    }

    public delegate void NetworkedAction(ref NetworkIdComponent nid, ref PositionComponent pos);
    public delegate void MovableAction(ref MovementQueueComponent q, ref PositionComponent pos, ref SpeedComponent spd);
    public delegate void StatsAction(ref StatsComponent stats);
    public delegate void PlayerAction(ref NetworkIdComponent nid, ref StatsComponent stats, ref SpeedComponent spd, ref SkillsComponent skills);

    /// <summary>Iterate all networked entities for state broadcast.</summary>
    public void ForEachNetworked(NetworkedAction action)
    {
        World.Query(in NetworkedQuery, (ref NetworkIdComponent nid, ref PositionComponent pos)
            => action(ref nid, ref pos));
    }

    /// <summary>Iterate all movable entities.</summary>
    public void ForEachMovable(MovableAction action)
    {
        World.Query(in MovableQuery,
            (ref MovementQueueComponent q, ref PositionComponent pos, ref SpeedComponent spd)
                => action(ref q, ref pos, ref spd));
    }

    /// <summary>Iterate all entities that have stats (for regen etc.).</summary>
    public void ForEachStats(StatsAction action)
    {
        World.Query(in StatsQuery, (ref StatsComponent stats) => action(ref stats));
    }

    /// <summary>Iterate all players (for stats/skills broadcast).</summary>
    public void ForEachPlayer(PlayerAction action)
    {
        World.Query(in PlayerQuery,
            (ref NetworkIdComponent nid, ref StatsComponent stats,
             ref SpeedComponent spd, ref SkillsComponent skills)
                => action(ref nid, ref stats, ref spd, ref skills));
    }

    public void Dispose() => Arch.Core.World.Destroy(World);
}
