using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Server.Combat;
using Server.ECS;
using Server.ECS.Components;
using Server.Maps;
using Shared;

namespace Server.ECS.Systems;

/// <summary>
/// Processes all player-vs-creature and creature-vs-player combat each tick.
///
/// Task 2.6: distance attacks now require Line of Sight.
/// Melee is exempt (Tibia behaviour – you can swing through a cracked wall).
/// When a ranged player has LoS blocked, they auto-walk toward the target.
/// </summary>
public sealed class CombatSystem : ISystem
{
    private readonly ServerWorld           _world;
    private readonly ILogger<CombatSystem> _logger;
    private readonly Random                _rng = new();
    private MapData?                       _mapData;

    private const float PlayerBaseAttackInterval = 2.0f;

    private static readonly QueryDescription PlayerCombatQuery = new QueryDescription()
        .WithAll<PlayerTag, PositionComponent, StatsComponent, SkillsComponent,
                 CombatComponent, MovementQueueComponent>();

    public CombatSystem(ServerWorld world, ILogger<CombatSystem> logger)
    {
        _world  = world;
        _logger = logger;
    }

    /// <summary>Called by GameLoop after the map is loaded.</summary>
    public void SetMapData(MapData map) => _mapData = map;

    public void Update(float deltaTime)
    {
        _world.World.Query(in PlayerCombatQuery,
            (Entity playerEntity,
             ref PositionComponent      pos,
             ref StatsComponent         stats,
             ref SkillsComponent        skills,
             ref CombatComponent        combat,
             ref MovementQueueComponent mq) =>
        {
            if (combat.AttackCooldown > 0f)
            {
                combat.AttackCooldown -= deltaTime;
                return;
            }

            if (combat.TargetEntity == Entity.Null
                || !combat.TargetEntity.IsAlive()
                || !combat.TargetEntity.Has<CreatureComponent>())
                return;

            ref var creature    = ref combat.TargetEntity.Get<CreatureComponent>();
            ref var creaturePos = ref combat.TargetEntity.Get<PositionComponent>();

            if (creaturePos.FloorZ != pos.FloorZ) return;

            int  dist       = ChebyshevDist(pos.TileX, pos.TileY, creaturePos.TileX, creaturePos.TileY);
            bool isDistance = combat.WeaponSkillType == WeaponSkillType.Distance;
            int  maxRange   = isDistance ? 7 : 1;

            // Out of range → auto-walk
            if (dist > maxRange)
            {
                QueueStepToward(ref pos, ref mq, creaturePos.TileX, creaturePos.TileY);
                return;
            }

            // Distance weapons require LoS
            if (isDistance && _mapData != null)
            {
                if (!LineOfSight.HasLoS(
                        pos.TileX, pos.TileY,
                        creaturePos.TileX, creaturePos.TileY,
                        pos.FloorZ, _mapData))
                {
                    QueueStepToward(ref pos, ref mq, creaturePos.TileX, creaturePos.TileY);
                    _logger.LogDebug("Player {PId}: LoS blocked – repositioning.", playerEntity.Id);
                    return;
                }
            }

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
                    skills.GetLevel(SkillType.Shielding),
                    combat.ShieldDefense,
                    creature.Armor,
                    _rng);
            }

            creature.CurrentHP = Math.Max(0, creature.CurrentHP - damage);
            _logger.LogDebug("Player {PId} deals {Dmg} to {Name} (HP {HP}/{Max})",
                playerEntity.Id, damage, creature.Name, creature.CurrentHP, creature.MaxHP);

            skills.AddTries(CombatFormulas.AttackSkillType(combat.WeaponSkillType), 1, stats.Vocation);
            combat.AttackCooldown = PlayerBaseAttackInterval;

            if (creature.CurrentHP <= 0)
            {
                OnCreatureDeath(ref playerEntity, ref stats, ref skills, combat.TargetEntity);
                combat.TargetEntity = Entity.Null;
            }
        });
    }

    // ── Called by CreatureAiSystem ────────────────────────────────────────

    public int ApplyCreatureAttack(ref CreatureComponent creature, Entity targetPlayer)
    {
        if (!targetPlayer.IsAlive()) return 0;
        if (!targetPlayer.Has<StatsComponent>()) return 0;

        ref var stats = ref targetPlayer.Get<StatsComponent>();

        int shieldSkill = targetPlayer.Has<SkillsComponent>()
            ? targetPlayer.Get<SkillsComponent>().GetLevel(SkillType.Shielding)
            : 0;

        int damage = CombatFormulas.CalculateCreatureMeleeDamage(
            creature.AttackMin, creature.AttackMax,
            shieldSkill, 0, _rng);

        bool died = stats.TakeDamage(damage);
        _logger.LogDebug("Creature {Name} deals {Dmg} to player (HP {HP}/{Max})",
            creature.Name, damage, stats.CurrentHP, stats.MaxHP);

        if (died) OnPlayerDeath(targetPlayer, ref stats);
        return damage;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void QueueStepToward(
        ref PositionComponent pos,
        ref MovementQueueComponent mq,
        int goalX, int goalY)
    {
        if (mq.QueuedDirection != (byte)Direction.None) return;
        int dx = goalX - pos.TileX;
        int dy = goalY - pos.TileY;
        mq.QueuedDirection = (byte)DirectionHelper.FromOffset(
            dx == 0 ? 0 : (dx > 0 ? 1 : -1),
            dy == 0 ? 0 : (dy > 0 ? 1 : -1));
    }

    private void OnCreatureDeath(
        ref Entity killerEntity,
        ref StatsComponent killerStats,
        ref SkillsComponent killerSkills,
        Entity creatureEntity)
    {
        if (!creatureEntity.Has<CreatureComponent>()) return;
        ref var creature = ref creatureEntity.Get<CreatureComponent>();

        bool leveledUp = killerStats.AddExperience(creature.Experience);
        _logger.LogInformation(
            "Creature {Name} died. Player {PId} gains {XP} XP (LvUp={LU})",
            creature.Name, killerEntity.Id, creature.Experience, leveledUp);

        if (creatureEntity.Has<CreatureAiComponent>())
        {
            ref var ai = ref creatureEntity.Get<CreatureAiComponent>();
            ai.State        = CreatureState.Dead;
            ai.TargetEntity = Arch.Core.Entity.Null;
        }
    }

    private void OnPlayerDeath(Entity playerEntity, ref StatsComponent stats)
    {
        _logger.LogInformation("Player {PId} died.", playerEntity.Id);
        stats.CurrentHP = stats.MaxHP;
        stats.IsDirty   = true;

        if (playerEntity.Has<CombatComponent>())
        {
            ref var combat = ref playerEntity.Get<CombatComponent>();
            combat.TargetEntity   = Arch.Core.Entity.Null;
            combat.AttackCooldown = 2f;
        }
    }

    private static int ChebyshevDist(int x1, int y1, int x2, int y2)
        => Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));
}
