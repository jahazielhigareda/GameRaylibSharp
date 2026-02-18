using Shared;

namespace Server.ECS.Components;

/// <summary>Arch struct component – creature/NPC base data.</summary>
public struct CreatureComponent
{
    public ushort  CreatureId;
    public int     CurrentHP;
    public int     MaxHP;
    public Vocation Vocation;       // used for NPCs that have vocations
    public bool    IsAggressive;
}
