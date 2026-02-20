using MessagePack;

namespace Shared.Packets;

// -----------------------------------------------------------------------------
// Full snapshot – sent to newly-connected clients or when a full resync is
// cheaper than a delta.
// -----------------------------------------------------------------------------

[MessagePackObject]
public class WorldStatePacket
{
    [Key(0)] public int                  Tick    { get; set; }
    [Key(1)] public List<PlayerSnapshot> Players { get; set; } = new();
}

// -----------------------------------------------------------------------------
// Delta snapshot types – only what changed since the last acked full/delta.
// -----------------------------------------------------------------------------

/// <summary>
/// One entry in a delta update.  A null field means "not changed".
/// Nullable value types keep the MessagePack payload small for partial updates.
/// </summary>
[MessagePackObject]
public class PlayerDeltaSnapshot
{
    [Key(0)] public int    Id    { get; set; }
    [Key(1)] public int?   TileX { get; set; }
    [Key(2)] public int?   TileY { get; set; }
    [Key(3)] public float? X     { get; set; }
    [Key(4)] public float? Y     { get; set; }
}

/// <summary>
/// Delta WorldState packet (PacketType.WorldDeltaPacket).
/// Computed by the server against the last snapshot it sent to this specific client.
/// </summary>
[MessagePackObject]
public class WorldDeltaPacket
{
    /// <summary>Server tick this delta corresponds to.</summary>
    [Key(0)] public int Tick { get; set; }

    /// <summary>Tick of the baseline snapshot this delta was computed from.</summary>
    [Key(1)] public int BaseTick { get; set; }

    /// <summary>Players whose state changed.</summary>
    [Key(2)] public List<PlayerDeltaSnapshot> Updated { get; set; } = new();

    /// <summary>Players who newly appeared (connected or entered interest area).</summary>
    [Key(3)] public List<PlayerSnapshot> Added { get; set; } = new();

    /// <summary>Ids of players who left (disconnected or left interest area).</summary>
    [Key(4)] public List<int> Removed { get; set; } = new();
}
