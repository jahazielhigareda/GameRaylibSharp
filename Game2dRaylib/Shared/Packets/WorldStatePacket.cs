using MessagePack;

namespace Shared.Packets;

[MessagePackObject]
public class WorldStatePacket
{
    [Key(0)] public int                   Tick    { get; set; }
    [Key(1)] public List<PlayerSnapshot>  Players { get; set; } = new();
}
