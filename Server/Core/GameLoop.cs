using Server.Creatures;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Server.ECS;
using Server.ECS.Components;
using Server.ECS.Systems;
using Server.Network;
using Shared;
using Server.Events;
using Server.Maps;
using Server.Spatial;
using Shared.Packets;
using System.Diagnostics;

namespace Server.Core;

public class GameLoop
{
    private readonly ILogger<GameLoop> _logger;
    private readonly ServerWorld       _world;
    private readonly MovementSystem    _movementSystem;
    private readonly CreatureAiSystem  _creatureAiSystem;
    private readonly CombatSystem      _combatSystem;
    private readonly StatsSystem       _statsSystem;
    private readonly NetworkManager    _networkManager;
    private readonly EventBus          _eventBus;
    private readonly SpatialHashGrid   _spatialGrid;
    private readonly MapLoader         _mapLoader;
    private readonly SpawnManager      _spawnManager;

    private MapData? _mapData;
    private int      _tick;
    private float    _statsTimer;
    private const float StatsBroadcastInterval = 1.0f;

    public GameLoop(
        ILogger<GameLoop> logger,
        ServerWorld world,
        MovementSystem movementSystem,
        StatsSystem statsSystem,
        NetworkManager networkManager,
        EventBus eventBus,
        SpatialHashGrid spatialGrid,
        MapLoader mapLoader,
        SpawnManager spawnManager,
        CreatureAiSystem creatureAiSystem,
        CombatSystem combatSystem)
    {
        _logger           = logger;
        _world            = world;
        _movementSystem   = movementSystem;
        _statsSystem      = statsSystem;
        _networkManager   = networkManager;
        _eventBus         = eventBus;
        _spatialGrid      = spatialGrid;
        _mapLoader        = mapLoader;
        _spawnManager     = spawnManager;
        _creatureAiSystem = creatureAiSystem;
        _combatSystem     = combatSystem;
        _spatialGrid.SetEventBus(eventBus);
    }

    public void Run(CancellationToken token)
    {
        string mapPath = Path.Combine(AppContext.BaseDirectory, "world.map");
        _mapData = _mapLoader.Load(mapPath);
        _movementSystem.SetMapData(_mapData);
        _creatureAiSystem.SetMapData(_mapData);
        _networkManager.SetMapData(_mapData);
        _movementSystem.NetworkManager = _networkManager;

        _spawnManager.SetAiSystem(_creatureAiSystem);
        _creatureAiSystem.SetCombatSystem(_combatSystem);
        _spawnManager.RegisterDefaultSpawns();
        _spawnManager.SpawnAll();

        const float targetDelta = 1f / Constants.TickRate;
        var sw = Stopwatch.StartNew();
        double accumulator = 0;

        _logger.LogInformation("Game loop started at {TickRate} ticks/s", Constants.TickRate);

        while (!token.IsCancellationRequested)
        {
            double elapsed = sw.Elapsed.TotalSeconds;
            sw.Restart();
            accumulator += elapsed;

            _networkManager.PollEvents();

            while (accumulator >= targetDelta)
            {
                _creatureAiSystem.Update((float)targetDelta);
                _combatSystem.Update((float)targetDelta);
                _movementSystem.Update((float)targetDelta);
                _spawnManager.Update((float)targetDelta);
                _statsSystem.Update((float)targetDelta);
                BroadcastState();
                BroadcastStatsIfNeeded((float)targetDelta);
                _tick++;
                accumulator -= targetDelta;
            }

            Thread.Sleep(1);
        }
    }

    private void BroadcastState()
    {
        // Rebuild spatial hash every tick
        _spatialGrid.Clear();
        _world.ForEachNetworked((ref NetworkIdComponent nid, ref PositionComponent pos) =>
        {
            var entity = _world.FindPlayer(nid.Id);
            if (entity != Arch.Core.Entity.Null)
                _spatialGrid.Add(entity, pos.TileX, pos.TileY);
        });

        // Build per-player WorldStatePacket containing only visible entities
        _world.ForEachNetworked((ref NetworkIdComponent observerNid, ref PositionComponent observerPos) =>
        {
            var packet = new WorldStatePacket { Tick = _tick };

            // Copy to locals – ref params can't be captured in nested lambdas
            int obsX = observerPos.TileX;
            int obsY = observerPos.TileY;
            byte obsZ = observerPos.FloorZ;

            // Add visible players
            foreach (var visibleEntity in _spatialGrid.GetVisible(obsX, obsY))
            {
                if (!visibleEntity.IsAlive()) continue;
                if (!visibleEntity.Has<NetworkIdComponent>()) continue;
                if (!visibleEntity.Has<PositionComponent>()) continue;

                ref var vNid = ref visibleEntity.Get<NetworkIdComponent>();
                ref var vPos = ref visibleEntity.Get<PositionComponent>();

                packet.Players.Add(new PlayerSnapshot
                {
                    Id         = vNid.Id,
                    TileX      = vPos.TileX,
                    TileY      = vPos.TileY,
                    X          = vPos.VisualX,
                    Y          = vPos.VisualY,
                    EntityType = Shared.Packets.SnapshotEntityType.Player,
                });
            }

            // Add visible creatures
            _world.World.Query(
                new Arch.Core.QueryDescription()
                    .WithAll<CreatureTag, CreatureNetworkIdComponent, PositionComponent, CreatureComponent>(),
                (Entity ce,
                 ref CreatureNetworkIdComponent cnid,
                 ref PositionComponent cpos,
                 ref CreatureComponent ccomp) =>
                {
                    if (cpos.FloorZ != obsZ) return;
                    int cdx = Math.Abs(cpos.TileX - obsX);
                    int cdy = Math.Abs(cpos.TileY - obsY);
                    if (cdx > Constants.ViewRange || cdy > Constants.ViewRange) return;
                    if (ccomp.CurrentHP <= 0) return;

                    byte hpPct = ccomp.MaxHP > 0
                        ? (byte)Math.Clamp(ccomp.CurrentHP * 100 / ccomp.MaxHP, 0, 100)
                        : (byte)0;

                    packet.Players.Add(new PlayerSnapshot
                    {
                        Id         = cnid.Id,
                        TileX      = cpos.TileX,
                        TileY      = cpos.TileY,
                        X          = cpos.VisualX,
                        Y          = cpos.VisualY,
                        EntityType = Shared.Packets.SnapshotEntityType.Creature,
                        HpPct      = hpPct,
                        CreatureId = ccomp.CreatureId,
                    });
                });

            _networkManager.SendWorldStateToPlayer(observerNid.Id, packet);
        });
    }

    private void BroadcastStatsIfNeeded(float deltaTime)
    {
        _statsTimer += deltaTime;
        if (_statsTimer < StatsBroadcastInterval) return;
        _statsTimer -= StatsBroadcastInterval;

        _world.ForEachPlayer((
            ref NetworkIdComponent nid,
            ref StatsComponent stats,
            ref SpeedComponent speed,
            ref SkillsComponent skills) =>
        {
            if (!stats.IsDirty) return;
            stats.IsDirty = false;

            var statsPacket = new StatsUpdatePacket
            {
                PlayerId    = nid.Id,
                Level       = stats.Level,
                Experience  = stats.Experience,
                ExpToNext   = stats.ExperienceToNextLevel(),
                CurrentHP   = stats.CurrentHP,
                MaxHP       = stats.MaxHP,
                CurrentMP   = stats.CurrentMP,
                MaxMP       = stats.MaxMP,
                Capacity    = stats.Capacity,
                MaxCapacity = stats.MaxCapacity,
                Soul        = stats.Soul,
                Stamina     = stats.Stamina,
                Vocation    = (byte)stats.Vocation,
                Speed       = speed.Speed
            };
            _networkManager.SendStatsToPlayer(nid.Id, statsPacket);

            if (skills.IsDirty)
            {
                skills.IsDirty = false;
                var voc = stats.Vocation;
                var skillsPacket = new SkillsUpdatePacket
                {
                    PlayerId         = nid.Id,
                    FistLevel        = skills.GetLevel(SkillType.Fist),
                    FistPercent      = skills.GetPercent(SkillType.Fist, voc),
                    ClubLevel        = skills.GetLevel(SkillType.Club),
                    ClubPercent      = skills.GetPercent(SkillType.Club, voc),
                    SwordLevel       = skills.GetLevel(SkillType.Sword),
                    SwordPercent     = skills.GetPercent(SkillType.Sword, voc),
                    AxeLevel         = skills.GetLevel(SkillType.Axe),
                    AxePercent       = skills.GetPercent(SkillType.Axe, voc),
                    DistanceLevel    = skills.GetLevel(SkillType.Distance),
                    DistancePercent  = skills.GetPercent(SkillType.Distance, voc),
                    ShieldingLevel   = skills.GetLevel(SkillType.Shielding),
                    ShieldingPercent = skills.GetPercent(SkillType.Shielding, voc),
                    FishingLevel     = skills.GetLevel(SkillType.Fishing),
                    FishingPercent   = skills.GetPercent(SkillType.Fishing, voc),
                    MagicLevel       = skills.GetLevel(SkillType.MagicLevel),
                    MagicPercent     = skills.GetPercent(SkillType.MagicLevel, voc)
                };
                _networkManager.SendSkillsToPlayer(nid.Id, skillsPacket);
            }
        });
    }
}
