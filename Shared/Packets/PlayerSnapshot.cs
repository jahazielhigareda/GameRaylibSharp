using MessagePack;

namespace Shared.Packets;

[MessagePackObject]
public class PlayerSnapshot
{
    [Key(0)] public int   Id    { get; set; }
    [Key(1)] public int   TileX { get; set; }   // Posición lógica en tiles
    [Key(2)] public int   TileY { get; set; }
    [Key(3)] public float X     { get; set; }    // Posición visual (para interpolación)
    [Key(4)] public float Y     { get; set; }
}
