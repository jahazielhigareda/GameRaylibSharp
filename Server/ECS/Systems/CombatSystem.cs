using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Server.Combat;
using Server.ECS;
using Server.ECS.Components;
using Shared;

namespace Server.ECS.Systems;

/// <summary>
/// Processes all player-vs-creature and creature-vs-player combat each tick.
///
/// Responsibilities:
///   1. Player attacks their targeted creature using Tibia melee/distance formulas.
///   2. Creature attacks targeted player (delegated from CreatureAiSystem via this system).
///   3. On creature death: award XP to the attacker, mark for respawn.
///   4. On player death: reset HP (simple respawn — full death system in later task).
///   5. Skill training for attacker's weapon skill and defender's shielding.
/// </summary>
public sealed class CombatSystem : ISystem
{
    private readonly ServerWorld            _world;
    private readonly ILogger<CombatSystem>  _logger;
    private readonly Random                 _rng = new();

    // ── Base attack speed ── players attack once every N seconds (base)
    private const float PlayerBaseAttackInterval = 2.0f;

    // ── Arch queries ─────────────────────────────────────────────────────
    private static readonly QueryDescription PlayerCombatQuery = new QueryDescription()
        .WithAll<PlayerTag, PositionComponent, StatsComponent, SkillsComponent, CombatComponent>();

    public CombatSystem(ServerWorld world, ILogger<CombatSystem> logger)
    {
        _world  = world;
        _logger = logger;
    }

    public void Update(float deltaTime)
    {
        _world.World.Query(in PlayerCombatQuery,
            (Entity playerEntity,
             ref PositionComponent pos,
             ref StatsComponent    stats,
             ref SkillsComponent   skills,
             ref CombatComponent   combat) =>
        {
            // Tick cooldown
            if (combat.AttackCooldown > 0f)
            {
                combat.AttackCooldown -= deltaTime;
                return;
            }

            // No target or dead target
            if (combat.TargetEntity == Entity.Null
                || !combat.TargetEntity.IsAlive()
                || !combat.TargetEntity.Has<CreatureComponent>())
                return;

            ref var creature    = ref combat.TargetEntity.Get<CreatureComponent>();
            ref var creaturePos = ref combat.TargetEntity.Get<PositionComponent>();

            // Same floor check
            if (creaturePos.FloorZ != pos.FloorZ) return;

            int dist = ChebyshevDist(pos.TileX, pos.TileY, creaturePos.TileX, creaturePos.TileY);

            // Determine attack type
            bool isDistance = combat.WeaponSkillType == WeaponSkillType.Distance;
            int maxRange    = isDistance ? 7 : 1;

            if (dist > maxRange) return; // too far — auto-walk handled by targeting system

            // ── Calculate damage ────────────────────────────────────────
            int damage;
            if (isDistance)
            {
                damage = CombatFormulas.CalculateDistanceDamage(
                    skills.GetLevel(SkillType.Distance),
                    combat.WeaponAttack,
                    dist,
                    creature.Armor,
                    _rng);
            }
            else
            {
                damage = CombatFormulas.CalculateMeleeDamage(
                    skills.GetLevel(CombatFormulas.AttackSkillType(combat.WeaponSkillType)),
                    combat.WeaponAttack,
                    skills.GetLevel(SkillType.Shielding), // defender uses own shielding
                    combat.ShieldDefense,
                    creature.Armor,
                    _rng);
            }

            // ── Apply damage to creature ─────────────────────────────────
            creature.CurrentHP = Math.Max(0, creature.CurrentHP - damage);
            _logger.LogDebug("Player {PId} deals {Dmg} to creature {Name} (HP {HP}/{MaxHP})",
                playerEntity.Id, damage, creature.Name, creature.CurrentHP, creature.MaxHP);

            // ── Skill training ──────────────────────────────────────────
            var weaponSkillType = CombatFormulas.AttackSkillType(combat.WeaponSkillType);
            skills.AddTries(weaponSkillType, 1, stats.Vocation);

            // Creature also trains player's shielding if it is in Attack state
            // (we check below at death — shielding is trained on being hit)

            // ── Reset attack cooldown ────────────────────────────────────
            combat.AttackCooldown = PlayerBaseAttackInterval;

            // ── Creature death ───────────────────────────────────────────
            if (creature.CurrentHP <= 0)
            {
                OnCreatureDeath(ref playerEntity, ref stats, ref skills, combat.TargetEntity);
                combat.TargetEntity = Entity.Null;
            }
        });
    }

    // ── Called by CreatureAiSystem when a creature attacks a player ───────

    /// <summary>
    /// Apply a creature's melee attack to a player.
    /// Called by <see cref="CreatureAiSystem"/> so all combat math lives here.
    /// Returns the damage dealt (0 if target is not a valid player).
    /// </summary>
    public int ApplyCreatureAttack(
        ref CreatureComponent creature,
        Entity playerEntity)
    {
        if (!playerEntity.IsAlive()) return 0;
        if (!playerEntity.Has<StatsComponent>()) return 0;
        if (!playerEntity.Has<SkillsComponent>()) return 0;

        ref var stats  = ref playerEntity.Get<StatsComponent>();
        ref var skills = ref playerEntity.Get<SkillsComponent>();

        int shieldSkill = skills.GetLevel(SkillType.Shielding);
        int armor       = playerEntity.Has<CombatComponent>()
                        ? playerEntity.Get<CombatComponent>().TotalArmor
                        : 0;

        int damage = CombatFormulas.CalculateCreatureMeleeDamage(
            creature.AttackMin,
            creature.AttackMax,
            shieldSkill,
            armor,
            _rng);

        bool died = stats.TakeDamage(damage);

        // Shielding skill training on being hit
        skills.AddTries(SkillType.Shielding, 1, stats.Vocation);

        _logger.LogDebug("Creature {Name} deals {Dmg} to player (HP {HP}/{MaxHP})",
            creature.Name, damage, stats.CurrentHP, stats.MaxHP);

        if (died)
            OnPlayerDeath(playerEntity, ref stats);

        return damage;
    }

    // ── Internal death handlers ───────────────────────────────────────────

    private void OnCreatureDeath(
        ref Entity killerEntity,
        ref StatsComponent killerStats,
        ref SkillsComponent killerSkills,
        Entity creatureEntity)
    {
        if (!creatureEntity.Has<CreatureComponent>()) return;
        ref var creature = ref creatureEntity.Get<CreatureComponent>();

        _logger.LogInformation("Creature {Name} died. Awarding {Exp} XP to player {PId}.",
            creature.Name, creature.Experience, killerEntity.Id);

        // Award experience
        bool leveledUp = killerStats.AddExperience(creature.Experience);
        if (leveledUp)
            _logger.LogInformation("Player {PId} leveled up to {Level}!",
                killerEntity.Id, killerStats.Level);

        // Mark creature as dead in AI component (SpawnManager handles respawn timer)
        if (creatureEntity.Has<CreatureAiComponent>())
        {
            ref var ai = ref creatureEntity.Get<CreatureAiComponent>();
            ai.State        = CreatureState.Dead;
            ai.TargetEntity = Entity.Null;
        }
    }

    private void OnPlayerDeath(Entity playerEntity, ref StatsComponent stats)
    {
        _logger.LogInformation("Player {PId} died.", playerEntity.Id);

        // Simple respawn: restore HP to full (full death system is a future task)
        stats.CurrentHP = stats.MaxHP;
        stats.IsDirty   = true;

        // Clear combat target
        if (playerEntity.Has<CombatComponent>())
        {
            ref var combat = ref playerEntity.Get<CombatComponent>();
            combat.TargetEntity   = Entity.Null;
            combat.AttackCooldown = 2f;
        }
    }

    private static int ChebyshevDist(int x1, int y1, int x2, int y2)
        => Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));
}
