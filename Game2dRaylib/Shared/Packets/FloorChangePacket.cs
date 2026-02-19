using MessagePack;

namespace Shared.Packets;

[MessagePackObject]
public class FloorChangePacket
{
    [Key(0)] public byte FromZ { get; set; }
    [Key(1)] public byte ToZ   { get; set; }
    [Key(2)] public int  X     { get; set; }
    [Key(3)] public int  Y     { get; set; }
}
