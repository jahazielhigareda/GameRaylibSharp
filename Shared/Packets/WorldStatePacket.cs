using MessagePack;

namespace Shared.Packets;

[MessagePackObject]
public class WorldStatePacket
{
    [Key(0)] public int                  Tick    { get; set; }
    [Key(1)] public List<PlayerSnapshot> Players { get; set; } = new();
}

[MessagePackObject]
public class PlayerDeltaSnapshot
{
    [Key(0)] public int    Id    { get; set; }
    [Key(1)] public int?   TileX { get; set; }
    [Key(2)] public int?   TileY { get; set; }
    [Key(3)] public float? X     { get; set; }
    [Key(4)] public float? Y     { get; set; }
    /// <summary>HP % (0-100). Null = unchanged. Populated for creatures when they take damage.</summary>
    [Key(5)] public byte?  HpPct { get; set; }
}

[MessagePackObject]
public class WorldDeltaPacket
{
    [Key(0)] public int Tick     { get; set; }
    [Key(1)] public int BaseTick { get; set; }
    [Key(2)] public List<PlayerDeltaSnapshot> Updated { get; set; } = new();
    [Key(3)] public List<PlayerSnapshot>      Added   { get; set; } = new();
    [Key(4)] public List<int>                 Removed { get; set; } = new();
}
