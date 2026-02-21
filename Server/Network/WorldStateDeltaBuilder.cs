using Shared.Packets;

namespace Server.Network;

/// <summary>
/// Computes full or delta world-state updates per client.
/// A full snapshot is sent on first connection or when >70% of entities changed.
/// </summary>
public static class WorldStateDeltaBuilder
{
    private const float DeltaThreshold = 0.7f;

    public static object BuildUpdate(WorldStatePacket current, PeerSessionState session)
    {
        if (session.LastSentSnapshot == null)
            return BuildFull(current, session);

        var prev = session.LastSentSnapshot;

        var prevIndex = new Dictionary<int, PlayerSnapshot>(prev.Players.Count);
        foreach (var p in prev.Players) prevIndex[p.Id] = p;

        var currIndex = new Dictionary<int, PlayerSnapshot>(current.Players.Count);
        foreach (var p in current.Players) currIndex[p.Id] = p;

        var updated = new List<PlayerDeltaSnapshot>();
        var added   = new List<PlayerSnapshot>();
        var removed = new List<int>();

        foreach (var p in current.Players)
        {
            if (!prevIndex.TryGetValue(p.Id, out var old)) { added.Add(p); continue; }
            var delta = ComputeDelta(old, p);
            if (delta != null) updated.Add(delta);
        }

        foreach (var p in prev.Players)
            if (!currIndex.ContainsKey(p.Id)) removed.Add(p.Id);

        int changes = updated.Count + added.Count + removed.Count;
        int total   = Math.Max(prevIndex.Count, currIndex.Count);

        if (total > 0 && (float)changes / total >= DeltaThreshold)
            return BuildFull(current, session);

        session.LastSentSnapshot = current;
        session.LastSentTick     = current.Tick;

        return new WorldDeltaPacket
        {
            Tick     = current.Tick,
            BaseTick = prev.Tick,
            Updated  = updated,
            Added    = added,
            Removed  = removed
        };
    }

    private static WorldStatePacket BuildFull(WorldStatePacket current, PeerSessionState session)
    {
        session.LastSentSnapshot = current;
        session.LastSentTick     = current.Tick;
        return current;
    }

    private static PlayerDeltaSnapshot? ComputeDelta(PlayerSnapshot old, PlayerSnapshot curr)
    {
        bool posChanged = old.TileX != curr.TileX || old.TileY != curr.TileY
                       || old.X     != curr.X     || old.Y     != curr.Y;
        bool hpChanged  = old.HpPct != curr.HpPct;

        if (!posChanged && !hpChanged) return null;

        return new PlayerDeltaSnapshot
        {
            Id    = curr.Id,
            TileX = posChanged ? curr.TileX : null,
            TileY = posChanged ? curr.TileY : null,
            X     = posChanged ? curr.X     : null,
            Y     = posChanged ? curr.Y     : null,
            HpPct = hpChanged  ? curr.HpPct : null,
        };
    }
}
