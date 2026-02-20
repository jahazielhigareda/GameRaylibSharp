using MessagePack;

namespace Shared.Packets;

[MessagePackObject]
public class JoinAcceptedPacket
{
    [Key(0)] public int AssignedId { get; set; }
}
