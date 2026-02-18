using MessagePack;

namespace Client.Network;

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

    public static (Shared.Packets.PacketType type, byte[] payload) Deserialize(byte[] data)
    {
        var type    = (Shared.Packets.PacketType)data[0];
        var payload = new byte[data.Length - 1];
        Buffer.BlockCopy(data, 1, payload, 0, payload.Length);
        return (type, payload);
    }

    private static Shared.Packets.PacketType GetPacketType<T>()
    {
        return typeof(T).Name switch
        {
            nameof(Shared.Packets.InputPacket)             => Shared.Packets.PacketType.InputPacket,
            nameof(Shared.Packets.WorldStatePacket)        => Shared.Packets.PacketType.WorldStatePacket,
            nameof(Shared.Packets.JoinAcceptedPacket)      => Shared.Packets.PacketType.JoinAcceptedPacket,
            nameof(Shared.Packets.PlayerDisconnectedPacket)=> Shared.Packets.PacketType.PlayerDisconnectedPacket,
            _ => throw new InvalidOperationException($"Unknown packet type: {typeof(T).Name}")
        };
    }
}
