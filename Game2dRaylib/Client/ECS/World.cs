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
                new StatsDataComponent(),
                new SkillsDataComponent());
        }

        return World.Create(
            new NetworkIdComponent { Id = networkId },
            new PositionComponent(),
            new RenderComponent { Color = color, Size = Constants.PlayerSize });
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
        int count = 0;
        World.Query(in PlayerQuery, (Entity _) => count++);
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
