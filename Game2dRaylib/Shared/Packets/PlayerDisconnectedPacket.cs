using MessagePack;

namespace Shared.Packets;

[MessagePackObject]
public class PlayerDisconnectedPacket
{
    [Key(0)] public int Id { get; set; }
}
