using Shared;
using Shared.Creatures;

namespace Server.ECS.Components;

/// <summary>
/// Arch struct component — creature/monster runtime data.
/// Stats are populated from <see cref="CreatureTemplate"/> at spawn time.
/// </summary>
public struct CreatureComponent
{
    public ushort           CreatureId;
    public string           Name;

    // ── Vitals ────────────────────────────────────────────────────────────
    public int              CurrentHP;
    public int              MaxHP;
    public int              CurrentMP;
    public int              MaxMP;

    // ── Combat stats ──────────────────────────────────────────────────────
    public int              AttackMin;
    public int              AttackMax;
    public int              Armor;      // Flat damage absorption
    public int              Defense;    // Shield-style reduction pool
    public int              Experience; // XP awarded on death

    // ── Behaviour ─────────────────────────────────────────────────────────
    public CreatureBehavior Behavior;
    public bool             IsAggressive;
    public int              LookRange;
    public int              ChaseRange;

    // ── Vocation (for NPCs that have one) ─────────────────────────────────
    public Vocation         Vocation;
}
