using Shared;

namespace Server.ECS.Components;

/// <summary>Arch struct component – projectile data.</summary>
public struct ProjectileComponent
{
    public int    OwnerNetId;
    public byte   ProjectileType;   // sprite/type id
    public int    TargetTileX;
    public int    TargetTileY;
    public float  Speed;
    public float  LifeTime;         // seconds remaining
}
