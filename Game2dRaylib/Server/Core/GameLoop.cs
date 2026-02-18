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
    private readonly NetworkManager     _networkManager;

    private int _tick;

    public GameLoop(ILogger<GameLoop> logger, World world, MovementSystem movementSystem, NetworkManager networkManager)
    {
        _logger         = logger;
        _world          = world;
        _movementSystem = movementSystem;
        _networkManager = networkManager;
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
                BroadcastState();
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
}
