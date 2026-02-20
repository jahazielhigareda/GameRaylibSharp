using Arch.Core;

namespace Server.ECS.Components;

/// <summary>
/// Arch struct component that holds all FSM and AI runtime state for a creature.
/// </summary>
public struct CreatureAiComponent
{
    // ── FSM ──────────────────────────────────────────────────────────────
    public CreatureState State;

    /// <summary>Accumulated time in the current state (used for ALERT delay).</summary>
    public float StateTimer;

    // ── Target tracking ───────────────────────────────────────────────────
    /// <summary>Entity this creature is currently targeting. Entity.Null if none.</summary>
    public Entity TargetEntity;

    // ── Attack cooldown ───────────────────────────────────────────────────
    /// <summary>Seconds until the next attack can be executed.</summary>
    public float AttackCooldown;

    // ── Idle wander ───────────────────────────────────────────────────────
    /// <summary>Cooldown until the next random idle move.</summary>
    public float WanderCooldown;

    // ── Spawn anchor (for RETURN state) ──────────────────────────────────
    public int SpawnX;
    public int SpawnY;
    public byte SpawnFloor;

    // ── Path cache (simple next-step target) ─────────────────────────────
    /// <summary>Next tile X the creature is walking toward (-1 = none).</summary>
    public int PathTargetX;
    public int PathTargetY;
}
