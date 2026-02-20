using Shared.Packets;

namespace Shared.Network;

/// <summary>
/// Central serialization façade.
///
/// Replaces the old single-byte-type prefix design with the full 5-byte header
/// (<see cref="NetworkPacket"/>).  Public API is backward-compatible in spirit:
/// callers still call Serialize / Deserialize, but they now go through the
/// versioned protocol pipeline and buffer pool.
///
/// Thread-safe: all state is immutable after static initialization.
/// </summary>
public static class PacketSerializer
{
    // Packet types that are LZ4-compressed on the wire.
    private static readonly HashSet<PacketType> CompressedTypes = new()
    {
        PacketType.WorldStatePacket,
        PacketType.WorldDeltaPacket,
        PacketType.MapDataPacket,
    };

    /// <summary>Shared registry – register additional handlers at startup if needed.</summary>
    public static readonly ProtocolHandlerRegistry Registry = new();

    static PacketSerializer()
    {
        Registry.Register(new ProtocolV1Handler());
    }

    // ── Build (serialize) ─────────────────────────────────────────────────

    /// <summary>
    /// Builds the full wire bytes for <paramref name="message"/>.
    /// Uses the current protocol version and auto-decides compression.
    /// Returns a newly allocated array safe to hand to LiteNetLib.
    /// </summary>
    public static byte[] Serialize<T>(T message)
    {
        var type      = GetPacketType<T>();
        bool compress = CompressedTypes.Contains(type);
        return BuildPacket(message, type, compress);
    }

    /// <summary>
    /// Explicit overload: caller chooses type and compression.
    /// </summary>
    public static byte[] BuildPacket<T>(T message, PacketType type, bool compress = false)
    {
        var handler = Registry.Current;
        var payload  = handler.SerializePayload(message, compress);
        var packet   = new NetworkPacket(type, payload, compress);

        // Rent a pooled buffer, write the framed packet, copy to exact-size array,
        // then return the buffer.  The ToArray() copy is unavoidable because
        // LiteNetLib takes ownership of the byte[] we pass it.
        using var buf = PacketBufferPool.Rent(packet.PacketLength);
        packet.WriteTo(buf.Span);
        return buf.Span.ToArray();
    }

    /// <summary>
    /// Writes directly into a caller-managed span.
    /// Use in hot paths where you control buffer lifetime.
    /// Returns the number of bytes written.
    /// </summary>
    public static int BuildPacketInto<T>(T message, PacketType type,
                                          Span<byte> destination,
                                          bool compress = false)
    {
        var handler = Registry.Current;
        var payload  = handler.SerializePayload(message, compress);
        var packet   = new NetworkPacket(type, payload, compress);
        return packet.WriteTo(destination);
    }

    // ── Parse (deserialize) ───────────────────────────────────────────────

    /// <summary>
    /// Parses a <see cref="NetworkPacket"/> from raw bytes received over the wire.
    /// </summary>
    public static NetworkPacket ParsePacket(byte[] data)
        => NetworkPacket.Parse(data.AsSpan());

    /// <summary>
    /// Parses and returns the (type, raw-payload) tuple.
    /// Kept for backward-compatibility with any existing switch dispatch.
    /// </summary>
    public static (PacketType type, byte[] payload) Deserialize(byte[] data)
    {
        var pkt = NetworkPacket.Parse(data.AsSpan());
        return (pkt.Type, pkt.Payload.ToArray());
    }

    /// <summary>
    /// Fully parses packet header and deserializes payload into
    /// <typeparamref name="T"/> using the appropriate protocol handler.
    /// </summary>
    public static T ParsePacket<T>(NetworkPacket packet)
    {
        var handler = Registry.Resolve(packet.ProtocolVersion);
        return handler.DeserializePayload<T>(packet.Payload, packet.IsCompressed);
    }

    /// <summary>Shortcut: parse wire bytes and immediately deserialize.</summary>
    public static T ParsePacket<T>(byte[] data)
        => ParsePacket<T>(NetworkPacket.Parse(data.AsSpan()));

    // ── Helpers ───────────────────────────────────────────────────────────

    private static PacketType GetPacketType<T>() =>
        typeof(T).Name switch
        {
            nameof(InputPacket)              => PacketType.InputPacket,
            nameof(MoveRequestPacket)        => PacketType.MoveRequestPacket,
            nameof(WorldStatePacket)         => PacketType.WorldStatePacket,
            nameof(WorldDeltaPacket)         => PacketType.WorldDeltaPacket,
            nameof(JoinAcceptedPacket)       => PacketType.JoinAcceptedPacket,
            nameof(PlayerDisconnectedPacket) => PacketType.PlayerDisconnectedPacket,
            nameof(StatsUpdatePacket)        => PacketType.StatsUpdatePacket,
            nameof(SkillsUpdatePacket)       => PacketType.SkillsUpdatePacket,
            nameof(FloorChangePacket)        => PacketType.FloorChangePacket,
            nameof(MapDataPacket)            => PacketType.MapDataPacket,
            _ => throw new InvalidOperationException($"Unknown packet type: {typeof(T).Name}")
        };
}
