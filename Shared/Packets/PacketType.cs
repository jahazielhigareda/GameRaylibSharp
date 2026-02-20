namespace Shared.Packets;

public enum PacketType : byte
{
    InputPacket              = 1,
    WorldStatePacket         = 2,   // full snapshot
    JoinAcceptedPacket       = 3,
    PlayerDisconnectedPacket = 4,
    MoveRequestPacket        = 5,
    StatsUpdatePacket        = 6,
    SkillsUpdatePacket       = 7,
    WorldDeltaPacket         = 8,   // delta snapshot
    FloorChangePacket        = 9,
    MapDataPacket            = 10,
    TargetRequestPacket      = 11,  // client → server: target a creature
}
