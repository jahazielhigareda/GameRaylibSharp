using Arch.Core;
using Shared;

namespace Server.ECS.Components;

/// <summary>
/// ECS component attached to player entities to track active combat state:
/// current target, attack cooldown, and equipped weapon stats.
///
/// Weapon stats are placeholders until the full inventory system (Task 3.x)
/// is implemented — defaulting to bare-fist values so combat works immediately.
/// </summary>
public struct CombatComponent
{
    // ── Target ────────────────────────────────────────────────────────────
    /// <summary>Currently targeted entity. <see cref="Entity.Null"/> if none.</summary>
    public Entity TargetEntity;

    // ── Attack cooldown ───────────────────────────────────────────────────
    /// <summary>Seconds until the player can attack again.</summary>
    public float AttackCooldown;

    // ── Weapon stats (populated from inventory; defaults = bare fist) ─────
    public int             WeaponAttack;      // Base attack value of equipped weapon
    public WeaponSkillType WeaponSkillType;   // Which skill is used for this weapon

    // ── Shield stats ──────────────────────────────────────────────────────
    public int ShieldDefense; // Base defense value of equipped shield

    // ── Armor ─────────────────────────────────────────────────────────────
    public int TotalArmor;    // Sum of all equipped armor pieces

    public CombatComponent()
    {
        TargetEntity    = Entity.Null;
        AttackCooldown  = 0f;
        WeaponAttack    = 10;   // Bare fist default
        WeaponSkillType = WeaponSkillType.Fist;
        ShieldDefense   = 0;    // 0 = no shield equipped; Shielding skill won't train without one
        TotalArmor      = 0;
    }
}
