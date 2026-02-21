using Server.ECS.Systems;
using Arch.Core;
using Microsoft.Extensions.Logging;
using Server.ECS;
using Server.Maps;
using Shared.Creatures;

namespace Server.Creatures;

/// <summary>
/// Manages creature spawn points: initial spawn on startup and
/// periodic respawn after a creature dies.
///
/// Canary equivalent: game/game.cpp placeCreature() + spawn XML loading.
/// </summary>
public sealed class SpawnManager
{
    private readonly ILogger<SpawnManager> _logger;
    private readonly ServerWorld           _world;
    private readonly CreatureDatabase      _db;
    private readonly MapLoader             _mapLoader;
    private CreatureAiSystem?              _aiSystem;
    private readonly Random                _rng = new();

    private readonly record struct SlotKey(int SpawnIndex, int SlotIndex);

    private readonly List<SpawnPoint>             _points    = new();
    private readonly Dictionary<SlotKey, Entity>  _alive     = new();
    private readonly Dictionary<SlotKey, float>   _respawnIn = new();

    public IReadOnlyList<SpawnPoint> SpawnPoints => _points;

    /// <summary>Wire in after DI resolution to allow path cache invalidation on death.</summary>
    public void SetAiSystem(CreatureAiSystem ai) => _aiSystem = ai;

    public SpawnManager(ILogger<SpawnManager> logger,
                        ServerWorld world,
                        CreatureDatabase db,
                        MapLoader mapLoader)
    {
        _logger    = logger;
        _world     = world;
        _db        = db;
        _mapLoader = mapLoader;
    }

    // ── Configuration ─────────────────────────────────────────────────────

    public void AddSpawnPoint(SpawnPoint point) => _points.Add(point);

    public void RegisterDefaultSpawns()
    {
        AddSpawnPoint(new SpawnPoint { CenterX = 10, CenterY = 10, Floor = 7, Radius = 3, CreatureName = "Rat",      MaxCount = 3, RespawnTime = 30  });
        AddSpawnPoint(new SpawnPoint { CenterX = 20, CenterY = 10, Floor = 7, Radius = 3, CreatureName = "Rat",      MaxCount = 3, RespawnTime = 30  });
        AddSpawnPoint(new SpawnPoint { CenterX = 10, CenterY = 20, Floor = 7, Radius = 3, CreatureName = "Goblin",   MaxCount = 2, RespawnTime = 60  });
        AddSpawnPoint(new SpawnPoint { CenterX = 25, CenterY = 25, Floor = 7, Radius = 4, CreatureName = "Troll",    MaxCount = 1, RespawnTime = 120 });
        AddSpawnPoint(new SpawnPoint { CenterX = 15, CenterY = 30, Floor = 7, Radius = 3, CreatureName = "Wolf",     MaxCount = 2, RespawnTime = 60  });
        AddSpawnPoint(new SpawnPoint { CenterX = 30, CenterY = 15, Floor = 7, Radius = 3, CreatureName = "Cave Rat", MaxCount = 2, RespawnTime = 45  });
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    public void SpawnAll()
    {
        for (int i = 0; i < _points.Count; i++)
        {
            var pt = _points[i];
            for (int slot = 0; slot < pt.MaxCount; slot++)
                DoSpawn(i, slot);
        }
        _logger.LogInformation("SpawnManager: {Count} creatures spawned.", _alive.Count);
    }

    public void Update(float deltaTime)
    {
        var toRespawn = new List<SlotKey>();

        foreach (var key in _respawnIn.Keys.ToList())
        {
            _respawnIn[key] -= deltaTime;
            if (_respawnIn[key] <= 0f)
                toRespawn.Add(key);
        }

        foreach (var key in toRespawn)
        {
            _respawnIn.Remove(key);
            DoSpawn(key.SpawnIndex, key.SlotIndex);
        }
    }

    public void NotifyDeath(Entity entity)
    {
        foreach (var key in _alive.Keys.ToList())
        {
            if (_alive[key] == entity)
            {
                _alive.Remove(key);
                var pt = _points[key.SpawnIndex];
                _respawnIn[key] = pt.RespawnTime;
                _aiSystem?.PathCache.Invalidate(entity.Id);
                _logger.LogInformation(
                    "Creature '{Name}' died. Slot {Key} will respawn in {T}s.",
                    pt.CreatureName, key, pt.RespawnTime);
                return;
            }
        }
    }

    // ── Internal helpers ──────────────────────────────────────────────────

    private void DoSpawn(int spawnIndex, int slotIndex)
    {
        var pt  = _points[spawnIndex];
        var key = new SlotKey(spawnIndex, slotIndex);

        if (!_db.TryGet(pt.CreatureName, out var template))
        {
            _logger.LogWarning("SpawnManager: unknown creature '{Name}'.", pt.CreatureName);
            return;
        }

        int attempts = 0;
        int tx, ty;
        do
        {
            tx = pt.CenterX + _rng.Next(-pt.Radius, pt.Radius + 1);
            ty = pt.CenterY + _rng.Next(-pt.Radius, pt.Radius + 1);
            attempts++;
        } while (!IsTileWalkable(tx, ty, pt.Floor) && attempts < 20);

        if (attempts >= 20)
        {
            _logger.LogWarning("SpawnManager: no walkable tile for '{Name}' near ({X},{Y}).",
                pt.CreatureName, pt.CenterX, pt.CenterY);
            _respawnIn[key] = 5f;
            return;
        }

        // ── BUG FIX: use the overload that copies ALL template fields ──────
        // The old overload (ushort creatureId, int tileX, int tileY, int maxHp, bool aggressive)
        // did NOT copy AttackMin/Max, LookRange, ChaseRange, Armor, Defense, Name, etc.
        // This caused creatures to have LookRange=0 (never aggro) and AttackMin/Max=0 (0 damage).
        var entity = _world.SpawnCreature(template, tx, ty, pt.Floor);

        _alive[key] = entity;
        _logger.LogDebug("Spawned {Name} at ({X},{Y}) floor {F} HP={HP} ATK={Min}-{Max} LookRange={LR}.",
            template.Name, tx, ty, pt.Floor, template.MaxHP, template.AttackMin, template.AttackMax, template.LookRange);
    }

    private bool IsTileWalkable(int tx, int ty, byte floor)
    {
        var map = _mapLoader.CurrentMap;
        if (map == null) return true;
        if (tx < 0 || tx >= map.Width || ty < 0 || ty >= map.Height) return false;
        return (map.Tiles[tx, ty, floor].Flags & Server.Maps.TileFlags.Walkable) != 0;
    }
}
