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
    private readonly StatsSystem       _statsSystem;
    private readonly NetworkManager    _networkManager;
    private readonly EventBus          _eventBus;
    private readonly SpatialHashGrid   _spatialGrid;
    private readonly MapLoader         _mapLoader;

    private MapData? _mapData;
    private int   _tick;
    private float _statsTimer;
    private const float StatsBroadcastInterval = 1.0f;

    public GameLoop(
        ILogger<GameLoop> logger,
        ServerWorld world,
        MovementSystem movementSystem,
        StatsSystem statsSystem,
        NetworkManager networkManager,
        EventBus eventBus,
        SpatialHashGrid spatialGrid,
        MapLoader mapLoader)
    {
        _logger         = logger;
        _world          = world;
        _movementSystem = movementSystem;
        _statsSystem    = statsSystem;
        _networkManager = networkManager;
        _eventBus       = eventBus;
        _spatialGrid    = spatialGrid;
        _mapLoader      = mapLoader;
        _spatialGrid.SetEventBus(eventBus);
    }

    public void Run(CancellationToken token)
    {
        // ── Load map ─────────────────────────────────────────────────────
        string mapPath = Path.Combine(AppContext.BaseDirectory, "world.map");
        _mapData = _mapLoader.Load(mapPath);
        _movementSystem.SetMapData(_mapData);
        _networkManager.SetMapData(_mapData);
        _movementSystem.NetworkManager = _networkManager;

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
                _movementSystem.Update((float)targetDelta);
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
        // ── Rebuild spatial hash every tick ───────────────────────────────
        _spatialGrid.Clear();
        _world.ForEachNetworked((ref NetworkIdComponent nid, ref PositionComponent pos) =>
        {
            var entity = _world.FindPlayer(nid.Id);
            if (entity != Arch.Core.Entity.Null)
                _spatialGrid.Add(entity, pos.TileX, pos.TileY);
        });

        // ── Build per-player WorldStatePacket containing only visible entities ──
        _world.ForEachNetworked((ref NetworkIdComponent observerNid, ref PositionComponent observerPos) =>
        {
            var packet = new WorldStatePacket { Tick = _tick };

            foreach (var visibleEntity in _spatialGrid.GetVisible(observerPos.TileX, observerPos.TileY))
            {
                if (!visibleEntity.IsAlive()) continue;
                if (!visibleEntity.Has<NetworkIdComponent>()) continue;
                if (!visibleEntity.Has<PositionComponent>()) continue;

                ref var vNid = ref visibleEntity.Get<NetworkIdComponent>();
                ref var vPos = ref visibleEntity.Get<PositionComponent>();

                packet.Players.Add(new PlayerSnapshot
                {
                    Id    = vNid.Id,
                    TileX = vPos.TileX,
                    TileY = vPos.TileY,
                    X     = vPos.VisualX,
                    Y     = vPos.VisualY
                });
            }

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
