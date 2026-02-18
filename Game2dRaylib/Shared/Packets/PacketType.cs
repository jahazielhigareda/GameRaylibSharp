namespace Shared.Packets;

public enum PacketType : byte
{
    InputPacket            = 1,
    WorldStatePacket       = 2,
    JoinAcceptedPacket     = 3,
    PlayerDisconnectedPacket = 4
}
