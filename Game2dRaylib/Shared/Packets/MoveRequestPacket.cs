using MessagePack;

namespace Shared.Packets;

[MessagePackObject]
public class MoveRequestPacket
{
    /// <summary>Direction: 0=North, 1=NE, 2=East, 3=SE, 4=South, 5=SW, 6=West, 7=NW</summary>
    [Key(0)] public byte Direction { get; set; }
    [Key(1)] public int  Tick      { get; set; }
}
