using LiteNetLib;
using LiteNetLib.Utils;
using MessagePack;
using Microsoft.Extensions.Logging;
using Client.ECS;
using Client.ECS.Entities;
using Client.ECS.Components;
using Client.Services;
using Shared;
using Shared.Network;
using Shared.Packets;

namespace Client.Network;

public class ClientNetworkManager : IDisposable
{
    private readonly EventBasedNetListener _listener;
    private readonly NetManager            _netManager;
    private readonly ILogger<ClientNetworkManager> _logger;
    private readonly World             _world;
    private readonly GameStateService  _state;

    private NetPeer? _server;

    public ClientNetworkManager(ILogger<ClientNetworkManager> logger, World world, GameStateService state)
    {
        _logger   = logger;
        _world    = world;
        _state    = state;
        _listener = new EventBasedNetListener();
        _netManager = new NetManager(_listener);

        _listener.PeerConnectedEvent    += OnConnected;
        _listener.PeerDisconnectedEvent += OnDisconnected;
        _listener.NetworkReceiveEvent   += OnReceive;
    }

    public void Connect()
    {
        _netManager.Start();
        _server = _netManager.Connect(Constants.ServerAddress, Constants.ServerPort, "");
        _logger.LogInformation("Connecting to {Address}:{Port}", Constants.ServerAddress, Constants.ServerPort);
    }

    public void PollEvents() => _netManager.PollEvents();

    public void SendMoveRequest(MoveRequestPacket packet)
    {
        if (_server == null) return;
        var writer = new NetDataWriter();
        writer.Put(PacketSerializer.Serialize(packet));
        _server.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void OnConnected(NetPeer peer)
        => _logger.LogInformation("Connected to server");

    private void OnDisconnected(NetPeer peer, DisconnectInfo info)
        => _logger.LogWarning("Disconnected: {Reason}", info.Reason);

    private void OnReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod delivery)
    {
        var data = reader.GetRemainingBytes();
        reader.Recycle();

        var (type, payload) = PacketSerializer.Deserialize(data);

        switch (type)
        {
            case PacketType.JoinAcceptedPacket:
                HandleJoinAccepted(payload);
                break;

            case PacketType.WorldStatePacket:
                HandleWorldState(payload);
                break;

            case PacketType.PlayerDisconnectedPacket:
                HandlePlayerDisconnected(payload);
                break;

            case PacketType.StatsUpdatePacket:
                HandleStatsUpdate(payload);
                break;

            case PacketType.SkillsUpdatePacket:
                HandleSkillsUpdate(payload);
                break;
        }
    }

    private void HandleJoinAccepted(byte[] payload)
    {
        var join = MessagePackSerializer.Deserialize<JoinAcceptedPacket>(payload);
        _state.LocalId = join.AssignedId;
        var localPlayer = new PlayerEntity(join.AssignedId, true);
        _world.AddEntity(localPlayer);
        _logger.LogInformation("Assigned ID: {Id}", join.AssignedId);
    }

    private void HandleWorldState(byte[] payload)
    {
        var ws = MessagePackSerializer.Deserialize<WorldStatePacket>(payload);
        _state.Tick = ws.Tick;

        foreach (var snap in ws.Players)
        {
            var entity = _world.GetEntitiesWith<NetworkIdComponent>()
                .FirstOrDefault(e => e.GetComponent<NetworkIdComponent>().Id == snap.Id);

            if (entity == null)
            {
                var remote = new PlayerEntity(snap.Id, snap.Id == _state.LocalId);
                _world.AddEntity(remote);
                entity = remote;

                var newPos = entity.GetComponent<PositionComponent>();
                newPos.SetFromServer(snap.TileX, snap.TileY, snap.X, snap.Y);
                newPos.SnapToTarget();
                continue;
            }

            var pos = entity.GetComponent<PositionComponent>();
            pos.SetFromServer(snap.TileX, snap.TileY, snap.X, snap.Y);
        }
    }

    private void HandlePlayerDisconnected(byte[] payload)
    {
        var disc = MessagePackSerializer.Deserialize<PlayerDisconnectedPacket>(payload);
        var toRemove = _world.GetEntitiesWith<NetworkIdComponent>()
            .FirstOrDefault(e => e.GetComponent<NetworkIdComponent>().Id == disc.Id);
        if (toRemove != null) _world.RemoveEntity(toRemove);
        _logger.LogInformation("Player {Id} left", disc.Id);
    }

    private void HandleStatsUpdate(byte[] payload)
    {
        var stats = MessagePackSerializer.Deserialize<StatsUpdatePacket>(payload);

        // Solo aplicar al jugador local
        if (stats.PlayerId != _state.LocalId) return;

        var localPlayer = _world.GetEntitiesWith<LocalPlayerComponent>().FirstOrDefault();
        if (localPlayer == null || !localPlayer.HasComponent<StatsDataComponent>()) return;

        var data = localPlayer.GetComponent<StatsDataComponent>();
        data.Level       = stats.Level;
        data.Experience  = stats.Experience;
        data.ExpToNext   = stats.ExpToNext;
        data.CurrentHP   = stats.CurrentHP;
        data.MaxHP       = stats.MaxHP;
        data.CurrentMP   = stats.CurrentMP;
        data.MaxMP       = stats.MaxMP;
        data.Capacity    = stats.Capacity;
        data.MaxCapacity = stats.MaxCapacity;
        data.Soul        = stats.Soul;
        data.Stamina     = stats.Stamina;
        data.Vocation    = stats.Vocation;
        data.Speed       = stats.Speed;
    }

    private void HandleSkillsUpdate(byte[] payload)
    {
        var skills = MessagePackSerializer.Deserialize<SkillsUpdatePacket>(payload);

        if (skills.PlayerId != _state.LocalId) return;

        var localPlayer = _world.GetEntitiesWith<LocalPlayerComponent>().FirstOrDefault();
        if (localPlayer == null || !localPlayer.HasComponent<SkillsDataComponent>()) return;

        var data = localPlayer.GetComponent<SkillsDataComponent>();
        data.FistLevel       = skills.FistLevel;
        data.FistPercent     = skills.FistPercent;
        data.ClubLevel       = skills.ClubLevel;
        data.ClubPercent     = skills.ClubPercent;
        data.SwordLevel      = skills.SwordLevel;
        data.SwordPercent    = skills.SwordPercent;
        data.AxeLevel        = skills.AxeLevel;
        data.AxePercent      = skills.AxePercent;
        data.DistanceLevel   = skills.DistanceLevel;
        data.DistancePercent = skills.DistancePercent;
        data.ShieldingLevel  = skills.ShieldingLevel;
        data.ShieldingPercent = skills.ShieldingPercent;
        data.FishingLevel    = skills.FishingLevel;
        data.FishingPercent  = skills.FishingPercent;
        data.MagicLevel      = skills.MagicLevel;
        data.MagicPercent    = skills.MagicPercent;
    }

    public void Dispose() => _netManager.Stop();
}
