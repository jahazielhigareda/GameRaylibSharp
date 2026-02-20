using MessagePack;

namespace Shared.Packets;

/// <summary>
/// Sent by the server to a newly-connected client so it can render the map.
/// Tiles are stored as parallel flat arrays (groundIds + flags) indexed
/// x + y*Width + z*Width*Height for compact MessagePack encoding.
/// </summary>
[MessagePackObject]
public class MapDataPacket
{
    [Key(0)] public ushort Width       { get; set; }
    [Key(1)] public ushort Height      { get; set; }
    [Key(2)] public byte   Floors      { get; set; }
    [Key(3)] public byte   GroundFloor { get; set; }
    /// <summary>groundItemId per cell, row-major [x + y*W + z*W*H].</summary>
    [Key(4)] public ushort[] GroundIds { get; set; } = Array.Empty<ushort>();
    /// <summary>TileFlags per cell, same layout as GroundIds.</summary>
    [Key(5)] public ushort[] Flags     { get; set; } = Array.Empty<ushort>();
}
