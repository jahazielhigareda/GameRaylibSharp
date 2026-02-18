using MessagePack;
using Shared.Packets;

namespace Shared.Network;

public static class PacketSerializer
{
    public static byte[] Serialize<T>(T packet)
    {
        var payload = MessagePackSerializer.Serialize(packet);
        var buffer  = new byte[payload.Length + 1];
        buffer[0]   = (byte)GetPacketType<T>();
        Buffer.BlockCopy(payload, 0, buffer, 1, payload.Length);
        return buffer;
    }

    public static (PacketType type, byte[] payload) Deserialize(byte[] data)
    {
        var type    = (PacketType)data[0];
        var payload = new byte[data.Length - 1];
        Buffer.BlockCopy(data, 1, payload, 0, payload.Length);
        return (type, payload);
    }

    private static PacketType GetPacketType<T>()
    {
        return typeof(T).Name switch
        {
            nameof(InputPacket)              => PacketType.InputPacket,
            nameof(MoveRequestPacket)        => PacketType.MoveRequestPacket,
            nameof(WorldStatePacket)         => PacketType.WorldStatePacket,
            nameof(JoinAcceptedPacket)       => PacketType.JoinAcceptedPacket,
            nameof(PlayerDisconnectedPacket) => PacketType.PlayerDisconnectedPacket,
            _ => throw new InvalidOperationException($"Unknown packet type: {typeof(T).Name}")
        };
    }
}
