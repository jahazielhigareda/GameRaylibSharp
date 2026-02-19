namespace Server.Maps;

[Flags]
public enum TileFlags : ushort
{
    None            = 0,
    Walkable        = 0x0001,
    BlockProjectile = 0x0002,
    ProtectionZone  = 0x0004,
    NoPvp           = 0x0008,
    NoLogout        = 0x0010,
    Refresh         = 0x0020,
}
