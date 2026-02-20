using Shared;

namespace Server.Combat;

/// <summary>
/// Pure static helpers implementing Tibia-style damage formulas.
/// No ECS references — purely mathematical so they can be unit-tested easily.
/// </summary>
public static class CombatFormulas
{
    // ── Melee ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Tibia melee damage formula.
    ///   maxDmg = (skill * attack * 0.085) + (skill * 0.085) + (attack * 0.085)
    /// Random roll: uniform 0..maxDmg.
    /// Defense reduction: random(defense*0.5, defense) + random(armor*0.5, armor).
    /// </summary>
    public static int CalculateMeleeDamage(
        int attackerSkill,
        int attackValue,
        int defenderShieldSkill,
        int defenderShieldValue,
        int defenderArmor,
        Random rng)
    {
        float maxDmg = (attackerSkill * attackValue * 0.085f)
                     + (attackerSkill * 0.085f)
                     + (attackValue   * 0.085f);

        float rawDmg = (float)(rng.NextDouble() * maxDmg);

        float blocked  = Lerp((float)rng.NextDouble(),
                              defenderShieldValue * 0.5f, defenderShieldValue)
                       + defenderShieldSkill * 0.08f;

        float absorbed = Lerp((float)rng.NextDouble(),
                              defenderArmor * 0.5f, defenderArmor);

        return Math.Max(0, (int)(rawDmg - blocked - absorbed));
    }

    /// <summary>
    /// Melee damage when attacker is a creature (uses raw AttackMin/Max range).
    /// Defender is a player with shielding skill and armor.
    /// </summary>
    public static int CalculateCreatureMeleeDamage(
        int creatureAttackMin,
        int creatureAttackMax,
        int defenderShieldSkill,
        int defenderArmor,
        Random rng)
    {
        if (creatureAttackMax <= creatureAttackMin) creatureAttackMin = 0;
        int rawDmg = rng.Next(creatureAttackMin, creatureAttackMax + 1);

        float blocked  = defenderShieldSkill * 0.08f;
        float absorbed = Lerp((float)rng.NextDouble(), defenderArmor * 0.5f, defenderArmor);

        return Math.Max(0, (int)(rawDmg - blocked - absorbed));
    }

    // ── Distance ──────────────────────────────────────────────────────────

    /// <summary>
    /// Distance attack with 2 % range penalty per tile.
    ///   maxDmg = skill * attack * 0.09 * (1 - distance * 0.02)
    /// </summary>
    public static int CalculateDistanceDamage(
        int attackerDistSkill,
        int attackValue,
        int distanceTiles,
        int defenderArmor,
        Random rng)
    {
        float distPenalty = Math.Max(0.2f, 1f - distanceTiles * 0.02f);
        float maxDmg      = attackerDistSkill * attackValue * 0.09f * distPenalty;
        float rawDmg      = (float)(rng.NextDouble() * maxDmg);
        float absorbed    = Lerp((float)rng.NextDouble(), defenderArmor * 0.5f, defenderArmor);

        return Math.Max(0, (int)(rawDmg - absorbed));
    }

    // ── Skill training ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the <see cref="SkillType"/> that should gain tries when a player
    /// attacks with the given weapon skill type.
    /// Falls back to <see cref="SkillType.Fist"/> for bare-handed.
    /// </summary>
    public static SkillType AttackSkillType(WeaponSkillType weaponSkill) => weaponSkill switch
    {
        WeaponSkillType.Club     => SkillType.Club,
        WeaponSkillType.Sword    => SkillType.Sword,
        WeaponSkillType.Axe      => SkillType.Axe,
        WeaponSkillType.Distance => SkillType.Distance,
        _                        => SkillType.Fist,
    };

    // ── Helpers ───────────────────────────────────────────────────────────

    private static float Lerp(float t, float min, float max)
        => min + t * (max - min);
}
