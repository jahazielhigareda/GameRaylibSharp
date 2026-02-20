using MessagePack;

namespace Shared.Packets;

/// <summary>Entity type discriminator inside a WorldStatePacket snapshot.</summary>
public enum SnapshotEntityType : byte
{
    Player   = 0,
    Creature = 1,
}

[MessagePackObject]
public class PlayerSnapshot
{
    [Key(0)] public int    Id         { get; set; }
    [Key(1)] public int    TileX      { get; set; }
    [Key(2)] public int    TileY      { get; set; }
    [Key(3)] public float  X          { get; set; }
    [Key(4)] public float  Y          { get; set; }
    /// <summary>Distinguishes players from creatures in the same list.</summary>
    [Key(5)] public SnapshotEntityType EntityType { get; set; } = SnapshotEntityType.Player;
    /// <summary>HP percentage 0-100 (for health bar rendering on client).</summary>
    [Key(6)] public byte   HpPct      { get; set; } = 100;
    /// <summary>Creature template ID (only meaningful when EntityType == Creature).</summary>
    [Key(7)] public ushort CreatureId { get; set; }
}
