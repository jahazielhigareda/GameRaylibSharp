using Microsoft.Extensions.Logging;
using Server.ECS;
using Server.ECS.Components;
using Server.ECS.Systems;
using Server.Network;
using Shared;
using Shared.Packets;
using System.Diagnostics;

namespace Server.Core;

public class GameLoop
{
    private readonly ILogger<GameLoop>  _logger;
    private readonly World              _world;
    private readonly MovementSystem     _movementSystem;
    private readonly StatsSystem        _statsSystem;
    private readonly NetworkManager     _networkManager;

    private int   _tick;
    private float _statsTimer;
    private const float StatsBroadcastInterval = 1.0f; // Enviar stats cada 1 segundo

    public GameLoop(
        ILogger<GameLoop> logger,
        World world,
        MovementSystem movementSystem,
        StatsSystem statsSystem,
        NetworkManager networkManager)
    {
        _logger          = logger;
        _world           = world;
        _movementSystem  = movementSystem;
        _statsSystem     = statsSystem;
        _networkManager  = networkManager;
    }

    public void Run(CancellationToken token)
    {
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
                _movementSystem.Update(targetDelta);
                _statsSystem.Update(targetDelta);
                BroadcastState();
                BroadcastStatsIfNeeded(targetDelta);
                _tick++;
                accumulator -= targetDelta;
            }

            Thread.Sleep(1);
        }
    }

    private void BroadcastState()
    {
        var packet = new WorldStatePacket { Tick = _tick };

        foreach (var entity in _world.GetEntitiesWith<NetworkIdComponent>())
        {
            var pos   = entity.GetComponent<PositionComponent>();
            var netId = entity.GetComponent<NetworkIdComponent>().Id;
            packet.Players.Add(new PlayerSnapshot
            {
                Id    = netId,
                TileX = pos.TileX,
                TileY = pos.TileY,
                X     = pos.VisualX,
                Y     = pos.VisualY
            });
        }

        _networkManager.BroadcastWorldState(packet);
    }

    private void BroadcastStatsIfNeeded(float deltaTime)
    {
        _statsTimer += deltaTime;
        if (_statsTimer < StatsBroadcastInterval) return;
        _statsTimer -= StatsBroadcastInterval;

        foreach (var entity in _world.GetEntitiesWith<StatsComponent>())
        {
            var stats = entity.GetComponent<StatsComponent>();
            var netId = entity.GetComponent<NetworkIdComponent>().Id;
            var speed = entity.GetComponent<SpeedComponent>();

            if (!stats.IsDirty) continue;
            stats.IsDirty = false;

            var statsPacket = new StatsUpdatePacket
            {
                PlayerId    = netId,
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

            _networkManager.SendStatsToPlayer(netId, statsPacket);

            // También enviar skills si están dirty
            if (entity.HasComponent<SkillsComponent>())
            {
                var skills = entity.GetComponent<SkillsComponent>();
                if (skills.IsDirty)
                {
                    skills.IsDirty = false;
                    var skillsPacket = new SkillsUpdatePacket
                    {
                        PlayerId        = netId,
                        FistLevel       = skills.GetLevel(SkillType.Fist),
                        FistPercent     = skills.GetPercent(SkillType.Fist, stats.Vocation),
                        ClubLevel       = skills.GetLevel(SkillType.Club),
                        ClubPercent     = skills.GetPercent(SkillType.Club, stats.Vocation),
                        SwordLevel      = skills.GetLevel(SkillType.Sword),
                        SwordPercent    = skills.GetPercent(SkillType.Sword, stats.Vocation),
                        AxeLevel        = skills.GetLevel(SkillType.Axe),
                        AxePercent      = skills.GetPercent(SkillType.Axe, stats.Vocation),
                        DistanceLevel   = skills.GetLevel(SkillType.Distance),
                        DistancePercent = skills.GetPercent(SkillType.Distance, stats.Vocation),
                        ShieldingLevel  = skills.GetLevel(SkillType.Shielding),
                        ShieldingPercent = skills.GetPercent(SkillType.Shielding, stats.Vocation),
                        FishingLevel    = skills.GetLevel(SkillType.Fishing),
                        FishingPercent  = skills.GetPercent(SkillType.Fishing, stats.Vocation),
                        MagicLevel      = skills.GetLevel(SkillType.MagicLevel),
                        MagicPercent    = skills.GetPercent(SkillType.MagicLevel, stats.Vocation)
                    };

                    _networkManager.SendSkillsToPlayer(netId, skillsPacket);
                }
            }
        }
    }
}
