namespace Shared.Packets;

public enum PacketType : byte
{
    InputPacket              = 1,
    WorldStatePacket         = 2,
    JoinAcceptedPacket       = 3,
    PlayerDisconnectedPacket = 4,
    MoveRequestPacket        = 5,
    StatsUpdatePacket        = 6,
    SkillsUpdatePacket       = 7
}
