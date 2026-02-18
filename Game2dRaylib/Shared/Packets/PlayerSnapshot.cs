using MessagePack;

namespace Shared.Packets;

[MessagePackObject]
public class PlayerSnapshot
{
    [Key(0)] public int   Id { get; set; }
    [Key(1)] public float X  { get; set; }
    [Key(2)] public float Y  { get; set; }
}
