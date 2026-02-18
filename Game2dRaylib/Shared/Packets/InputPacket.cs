using MessagePack;

namespace Shared.Packets;

[MessagePackObject]
public class InputPacket
{
    [Key(0)] public bool Up    { get; set; }
    [Key(1)] public bool Down  { get; set; }
    [Key(2)] public bool Left  { get; set; }
    [Key(3)] public bool Right { get; set; }
    [Key(4)] public int  Tick  { get; set; }
}
