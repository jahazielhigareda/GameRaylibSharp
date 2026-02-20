using Shared;
using Shared.Creatures;

namespace Server.ECS.Components;

/// <summary>Arch struct component - creature/NPC runtime data.</summary>
public struct CreatureComponent
{
    public ushort           CreatureId;
    public int              CurrentHP;
    public int              MaxHP;
    public int              CurrentMP;
    public int              MaxMP;
    public int              Experience;
    public int              AttackMin;
    public int              AttackMax;
    public int              Armor;
    public int              Defense;
    public int              LookRange;
    public int              ChaseRange;
    public CreatureBehavior Behavior;
    public bool             IsAggressive;
    public Vocation         Vocation;
}
