using Arch.Core;
using Arch.Core.Extensions;
using Client.ECS.Components;
using Raylib_cs;
using Shared;

namespace Client.ECS;

/// <summary>
/// Arch-backed ECS world for the client.
/// </summary>
public sealed class ClientWorld : IDisposable
{
    public readonly Arch.Core.World World;

    public ClientWorld() { World = Arch.Core.World.Create(); }

    // ── Archetype queries ─────────────────────────────────────────────────

    private static readonly QueryDescription PlayerQuery = new QueryDescription()
        .WithAll<NetworkIdComponent, PositionComponent, RenderComponent>();

    private static readonly QueryDescription LocalPlayerQuery = new QueryDescription()
        .WithAll<LocalPlayerComponent, PositionComponent>();

    private static readonly QueryDescription LocalStatsQuery = new QueryDescription()
        .WithAll<LocalPlayerComponent, StatsDataComponent>();

    private static readonly QueryDescription LocalSkillsQuery = new QueryDescription()
        .WithAll<LocalPlayerComponent, SkillsDataComponent>();

    private static readonly QueryDescription InterpolationQuery = new QueryDescription()
        .WithAll<PositionComponent>();

    private static readonly QueryDescription RenderQuery = new QueryDescription()
        .WithAll<PositionComponent, RenderComponent>();

    private static readonly QueryDescription CreatureQuery = new QueryDescription()
        .WithAll<NetworkIdComponent, PositionComponent, RenderComponent, CreatureClientTag>();

    private static readonly QueryDescription EffectQuery = new QueryDescription()
        .WithAll<PositionComponent, EffectComponent>();

    // ── Entity creation ───────────────────────────────────────────────────

    /// <summary>Spawn (or re-use) a player entity for a given network id.</summary>
    public Entity SpawnPlayer(int networkId, bool isLocal)
    {
        var color = isLocal ? Color.Blue : Color.Red;

        if (isLocal)
        {
            return World.Create(
                new LocalPlayerComponent(),
                new NetworkIdComponent { Id = networkId },
                new PositionComponent(),
                new RenderComponent { Color = color, Size = Constants.PlayerSize },
                new CreatureRenderOrder(),
                new StatsDataComponent(),
                new SkillsDataComponent());
        }

        return World.Create(
            new NetworkIdComponent { Id = networkId },
            new PositionComponent(),
            new RenderComponent { Color = color, Size = Constants.PlayerSize },
            new CreatureRenderOrder());
    }


    /// <summary>Creates or updates a remote creature entity from a server snapshot.</summary>
    public void UpsertCreature(int netId, int tileX, int tileY, float x, float y, byte hpPct, string name = "")
    {
        // Try to find existing
        Entity found = Entity.Null;
        World.Query(in CreatureQuery, (Entity e, ref NetworkIdComponent nid) =>
        {
            if (nid.Id == netId) found = e;
        });

        if (found == Entity.Null)
        {
            found = World.Create(
                new NetworkIdComponent { Id = netId },
                new PositionComponent(),
                new RenderComponent { Color = Raylib_cs.Color.Orange, Size = Constants.CreatureSize },
                new CreatureRenderOrder(),
                new CreatureClientTag(),
                new CreatureHpComponent(),
                new CreatureNameComponent());
        }

        ref var pos = ref found.Get<PositionComponent>();
        pos.SetFromServer(tileX, tileY, x, y);
        if (pos.X == 0 && pos.Y == 0) pos.SnapToTarget();

        if (found.Has<CreatureHpComponent>())
            found.Get<CreatureHpComponent>().HpPct = hpPct;

        if (found.Has<CreatureNameComponent>() && !string.IsNullOrEmpty(name))
        {
            ref var nm = ref found.Get<CreatureNameComponent>();
            nm.Name = name;
        }
    }

    /// <summary>Find a creature entity by its network ID.</summary>
    public Entity FindCreature(int netId)
    {
        Entity found = Entity.Null;
        World.Query(in CreatureQuery, (Entity e, ref NetworkIdComponent nid) =>
        {
            if (nid.Id == netId) found = e;
        });
        return found;
    }

    public void RemoveCreature(int netId)
    {
        Entity found = Entity.Null;
        World.Query(in CreatureQuery, (Entity e, ref NetworkIdComponent nid) =>
        {
            if (nid.Id == netId) found = e;
        });
        if (found != Entity.Null) World.Destroy(found);
    }

    public void DestroyEntity(Entity entity) => World.Destroy(entity);

    // ── Lookup helpers ────────────────────────────────────────────────────

    /// <summary>Find a player by network id. Returns Entity.Null if not found.</summary>
    public Entity FindPlayer(int networkId)
    {
        Entity found = Entity.Null;
        World.Query(in PlayerQuery, (Entity e, ref NetworkIdComponent nid) =>
        {
            if (nid.Id == networkId) found = e;
        });
        return found;
    }

    /// <summary>Returns true if there is at least one local player entity.</summary>
    public bool TryGetLocalPlayer(out Entity entity)
    {
        entity = Entity.Null;
        Entity tmp = Entity.Null;
        World.Query(in LocalPlayerQuery, (Entity e) => { tmp = e; });
        if (tmp == Entity.Null) return false;
        entity = tmp;
        return true;
    }

    public int CountPlayers()
    {
        // Exclude creatures � they also match PlayerQuery (same components)
        int count = 0;
        World.Query(in PlayerQuery, (Entity e) =>
        {
            if (!e.Has<CreatureClientTag>()) count++;
        });
        return count;
    }

    // ── Iteration helpers ─────────────────────────────────────────────────

    public delegate void InterpolationAction(ref PositionComponent pos);
    public delegate void RenderableAction(Entity e, ref PositionComponent pos, ref RenderComponent render);

    public void ForEachInterpolation(InterpolationAction action)
    {
        World.Query(in InterpolationQuery, (ref PositionComponent pos) => action(ref pos));
    }

    public void ForEachRenderable(RenderableAction action)
    {
        World.Query(in RenderQuery, (Entity e, ref PositionComponent p, ref RenderComponent r)
            => action(e, ref p, ref r));
    }

    public void Dispose() => Arch.Core.World.Destroy(World);
}
