#!/bin/bash
set -e

echo "Applying Task 2.4 — Combat System..."

# ──────────────────────────────────────────────────────────────────────────────
# 1. Update CreatureComponent — add all combat stats referenced by CombatSystem
# ──────────────────────────────────────────────────────────────────────────────

echo "Updating Server/ECS/Components/CreatureComponent.cs..."
cat << 'EOF' > Server/ECS/Components/CreatureComponent.cs
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
EOF

# ──────────────────────────────────────────────────────────────────────────────
# 2. CombatFormulas — pure static class with Tibia-style formulae
# ──────────────────────────────────────────────────────────────────────────────

echo "Creating Server/Combat/CombatFormulas.cs..."
mkdir -p Server/Combat

cat << 'EOF' > Server/Combat/CombatFormulas.cs
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
EOF

# ──────────────────────────────────────────────────────────────────────────────
# 3. WeaponSkillType enum (Shared — referenced by formulas and combat component)
# ──────────────────────────────────────────────────────────────────────────────

echo "Creating Shared/WeaponSkillType.cs..."
cat << 'EOF' > Shared/WeaponSkillType.cs
namespace Shared;

/// <summary>
/// The weapon skill category used for damage rolls and skill training.
/// Matches the physical weapon types that map to SkillType entries.
/// </summary>
public enum WeaponSkillType : byte
{
    Fist     = 0,
    Club     = 1,
    Sword    = 2,
    Axe      = 3,
    Distance = 4,
}
EOF

# ──────────────────────────────────────────────────────────────────────────────
# 4. CombatComponent — player's active combat state (target, cooldown, weapon)
# ──────────────────────────────────────────────────────────────────────────────

echo "Creating Server/ECS/Components/CombatComponent.cs..."
cat << 'EOF' > Server/ECS/Components/CombatComponent.cs
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
        ShieldDefense   = 5;    // No shield default
        TotalArmor      = 0;
    }
}
EOF

# ──────────────────────────────────────────────────────────────────────────────
# 5. CombatSystem — main server system processing all combat this tick
# ──────────────────────────────────────────────────────────────────────────────

echo "Creating Server/ECS/Systems/CombatSystem.cs..."
cat << 'EOF' > Server/ECS/Systems/CombatSystem.cs
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
EOF

# ──────────────────────────────────────────────────────────────────────────────
# 6. Update CreatureAiSystem.ExecuteAttack to delegate to CombatSystem
# ──────────────────────────────────────────────────────────────────────────────

echo "Updating CreatureAiSystem to delegate damage to CombatSystem..."
python3 - << 'PYEOF'
path = "Server/ECS/Systems/CreatureAiSystem.cs"
with open(path, "r") as f:
    src = f.read()

if "_combatSystem" not in src:
    # Add field
    src = src.replace(
        "    private readonly PathCache                _pathCache = new();",
        "    private readonly PathCache                _pathCache = new();\n    private CombatSystem?                 _combatSystem;"
    )
    # Add exposed property / setter
    src = src.replace(
        "    /// <summary>Exposed so SpawnManager can invalidate on creature death.</summary>\n    public PathCache PathCache => _pathCache;",
        "    /// <summary>Exposed so SpawnManager can invalidate on creature death.</summary>\n    public PathCache PathCache => _pathCache;\n\n    /// <summary>Wire in after DI to delegate attack damage to CombatSystem.</summary>\n    public void SetCombatSystem(CombatSystem cs) => _combatSystem = cs;"
    )
    # Replace raw ExecuteAttack body
    old_body = '''    private void ExecuteAttack(ref CreatureComponent creature, ref CreatureAiComponent ai)
    {
        if (!ai.TargetEntity.IsAlive()) return;
        if (!ai.TargetEntity.Has<StatsComponent>()) return;

        int dmg = _rng.Next(creature.AttackMin, creature.AttackMax + 1);
        ref var targetStats = ref ai.TargetEntity.Get<StatsComponent>();
        targetStats.CurrentHP = Math.Max(0, targetStats.CurrentHP - dmg);

        _logger.LogDebug("Creature {Id} deals {Dmg} dmg (target HP={HP})",
            creature.CreatureId, dmg, targetStats.CurrentHP);
    }'''

    new_body = '''    private void ExecuteAttack(ref CreatureComponent creature, ref CreatureAiComponent ai)
    {
        if (!ai.TargetEntity.IsAlive()) return;

        if (_combatSystem != null)
        {
            _combatSystem.ApplyCreatureAttack(ref creature, ai.TargetEntity);
        }
        else
        {
            // Fallback if CombatSystem not yet wired (e.g. during tests)
            if (!ai.TargetEntity.Has<StatsComponent>()) return;
            int dmg = _rng.Next(creature.AttackMin, creature.AttackMax + 1);
            ref var targetStats = ref ai.TargetEntity.Get<StatsComponent>();
            targetStats.CurrentHP = Math.Max(0, targetStats.CurrentHP - dmg);
        }
    }'''

    if old_body in src:
        src = src.replace(old_body, new_body)
    else:
        print("WARNING: ExecuteAttack body pattern not found exactly — skipping body replacement.")

    with open(path, "w") as f:
        f.write(src)
    print("CreatureAiSystem updated.")
else:
    print("Already patched.")
PYEOF

# ──────────────────────────────────────────────────────────────────────────────
# 7. Patch ServerWorld.SpawnPlayer to attach CombatComponent
# ──────────────────────────────────────────────────────────────────────────────

echo "Patching ServerWorld.SpawnPlayer to attach CombatComponent..."
python3 - << 'PYEOF'
path = "Server/ECS/World.cs"
with open(path, "r") as f:
    src = f.read()

if "CombatComponent" not in src:
    src = src.replace(
        "            stats,\n            new SkillsComponent());",
        "            stats,\n            new SkillsComponent(),\n            new CombatComponent());"
    )
    with open(path, "w") as f:
        f.write(src)
    print("CombatComponent added to SpawnPlayer.")
else:
    print("Already present.")
PYEOF

# ──────────────────────────────────────────────────────────────────────────────
# 8. Register CombatSystem in Program.cs
# ──────────────────────────────────────────────────────────────────────────────

echo "Registering CombatSystem in Server/Program.cs..."
python3 - << 'PYEOF'
path = "Server/Program.cs"
with open(path, "r") as f:
    src = f.read()

if "CombatSystem" not in src:
    src = src.replace(
        "services.AddSingleton<CreatureAiSystem>();",
        "services.AddSingleton<CreatureAiSystem>();\nservices.AddSingleton<CombatSystem>();"
    )
    with open(path, "w") as f:
        f.write(src)
    print("CombatSystem registered.")
else:
    print("Already registered.")
PYEOF

# ──────────────────────────────────────────────────────────────────────────────
# 9. Wire CombatSystem into GameLoop
# ──────────────────────────────────────────────────────────────────────────────

echo "Wiring CombatSystem into Server/Core/GameLoop.cs..."
python3 - << 'PYEOF'
path = "Server/Core/GameLoop.cs"
with open(path, "r") as f:
    src = f.read()

changed = False

# Add field
if "_combatSystem" not in src:
    src = src.replace(
        "    private readonly CreatureAiSystem   _creatureAiSystem;",
        "    private readonly CreatureAiSystem   _creatureAiSystem;\n    private readonly CombatSystem       _combatSystem;"
    )
    changed = True

# Add ctor param
if "CombatSystem combatSystem" not in src:
    src = src.replace(
        "        CreatureAiSystem creatureAiSystem)",
        "        CreatureAiSystem creatureAiSystem,\n        CombatSystem combatSystem)"
    )
    changed = True

# Assign field
if "_combatSystem   = combatSystem" not in src:
    src = src.replace(
        "        _creatureAiSystem   = creatureAiSystem;",
        "        _creatureAiSystem   = creatureAiSystem;\n        _combatSystem      = combatSystem;"
    )
    changed = True

# Wire combat system into AI after startup
if "SetCombatSystem" not in src:
    src = src.replace(
        "        _spawnManager.SetAiSystem(_creatureAiSystem);",
        "        _spawnManager.SetAiSystem(_creatureAiSystem);\n        _creatureAiSystem.SetCombatSystem(_combatSystem);"
    )
    changed = True

# Tick CombatSystem in game loop (after AI, before movement)
if "_combatSystem.Update" not in src:
    src = src.replace(
        "            _creatureAiSystem.Update((float)targetDelta);",
        "            _creatureAiSystem.Update((float)targetDelta);\n            _combatSystem.Update((float)targetDelta);"
    )
    changed = True

with open(path, "w") as f:
    f.write(src)
print("GameLoop.cs updated." if changed else "Already up to date.")
PYEOF

# ──────────────────────────────────────────────────────────────────────────────
# 10. Update World.cs SpawnCreature (CreatureTemplate overload) to fill new fields
# ──────────────────────────────────────────────────────────────────────────────

echo "Updating SpawnCreature(CreatureTemplate) in World.cs..."
python3 - << 'PYEOF'
path = "Server/ECS/World.cs"
with open(path, "r") as f:
    src = f.read()

# Look for the template overload and ensure it populates the new fields
old = "                Behavior     = template.Behavior,\n                IsAggressive = template.Behavior != Shared.Creatures.CreatureBehavior.Passive,"
new = "                Behavior     = template.Behavior,\n                IsAggressive = template.Behavior != Shared.Creatures.CreatureBehavior.Passive,\n                AttackMin    = template.AttackMin,\n                AttackMax    = template.AttackMax,\n                Armor        = template.Armor,\n                Defense      = template.Defense,\n                Experience   = template.Experience,\n                LookRange    = template.LookRange,\n                ChaseRange   = template.ChaseRange,\n                Name         = template.Name,"

if old in src and "AttackMin    = template.AttackMin" not in src:
    src = src.replace(old, new)
    with open(path, "w") as f:
        f.write(src)
    print("SpawnCreature(template) updated.")
else:
    print("Already updated or pattern not found.")
PYEOF

# ──────────────────────────────────────────────────────────────────────────────

echo "Restoring packages..."
dotnet restore

echo "Building solution..."
dotnet build Game2dRaylib.sln

echo "Changes applied successfully."