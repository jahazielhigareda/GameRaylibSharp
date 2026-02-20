using Server.Maps;

namespace Server.ECS.Components;

/// <summary>ECS component for scripted / interactive tiles.</summary>
public struct TileComponent
{
    public ushort    GroundItemId;
    public TileFlags Flags;
    public int       TileX;
    public int       TileY;
    public byte      Floor;

    public bool IsWalkable => (Flags & TileFlags.Walkable) != 0;
}
