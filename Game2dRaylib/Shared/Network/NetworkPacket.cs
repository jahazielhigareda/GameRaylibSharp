using Shared.Packets;

namespace Shared.Network;

/// <summary>
/// Low-level framed packet.
///
/// Wire layout:
///   [0..1] PacketLength    – ushort, total bytes including this header (5 + payload)
///   [2]    PacketType      – byte
///   [3]    ProtocolVersion – byte
///   [4]    Flags           – byte  (bit 0 = IsCompressed, bit 1 = HasSequenceNumber)
///   [5..n] Payload         – MessagePack bytes (optionally LZ4-compressed)
///
/// Header is 5 bytes.  MinLength == 5 (empty payload).
/// </summary>
public readonly struct NetworkPacket
{
    public const int  HeaderSize     = 5;
    public const byte CurrentVersion = 1;

    // Flag bits
    public const byte FlagCompressed     = 0x01;
    public const byte FlagHasSequenceNum = 0x02;

    public readonly ushort               PacketLength;    // full wire length (header + payload)
    public readonly PacketType           Type;
    public readonly byte                 ProtocolVersion;
    public readonly byte                 Flags;
    public readonly ReadOnlyMemory<byte> Payload;

    public bool IsCompressed      => (Flags & FlagCompressed)     != 0;
    public bool HasSequenceNumber => (Flags & FlagHasSequenceNum) != 0;

    public NetworkPacket(PacketType type, byte[] payload,
                         bool compress = false,
                         byte version  = CurrentVersion)
    {
        Type            = type;
        ProtocolVersion = version;
        Flags           = compress ? FlagCompressed : (byte)0;
        Payload         = payload;
        PacketLength    = checked((ushort)(HeaderSize + payload.Length));
    }

    // Internal constructor used by Parse() – payload may be a slice of a rented buffer.
    internal NetworkPacket(PacketType type, byte version, byte flags,
                           ReadOnlyMemory<byte> payload, ushort packetLength)
    {
        Type            = type;
        ProtocolVersion = version;
        Flags           = flags;
        Payload         = payload;
        PacketLength    = packetLength;
    }

    /// <summary>
    /// Writes the packet into <paramref name="destination"/> (must be >= PacketLength bytes).
    /// Returns the number of bytes written.
    /// </summary>
    public int WriteTo(Span<byte> destination)
    {
        if (destination.Length < PacketLength)
            throw new ArgumentException("Destination buffer too small.");

        destination[0] = (byte)(PacketLength & 0xFF);
        destination[1] = (byte)(PacketLength >> 8);
        destination[2] = (byte)Type;
        destination[3] = ProtocolVersion;
        destination[4] = Flags;
        Payload.Span.CopyTo(destination[HeaderSize..]);
        return PacketLength;
    }

    /// <summary>
    /// Parses a <see cref="NetworkPacket"/> from a raw byte span.
    /// Throws <see cref="InvalidDataException"/> on malformed data.
    /// </summary>
    public static NetworkPacket Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize)
            throw new InvalidDataException(
                $"Packet too short: {data.Length} < {HeaderSize}");

        ushort length  = (ushort)(data[0] | (data[1] << 8));
        var    type    = (PacketType)data[2];
        byte   version = data[3];
        byte   flags   = data[4];

        if (data.Length < length)
            throw new InvalidDataException(
                $"Truncated packet: declared {length}, got {data.Length}");

        // Copy payload out so the caller may return any rented buffer safely.
        var payload = data[HeaderSize..length].ToArray();
        return new NetworkPacket(type, version, flags, payload, length);
    }
}
