using LiteNetLib;
using LiteNetLib.Utils;
using MessagePack;
using Microsoft.Extensions.Logging;
using Client.ECS;
using Client.ECS.Entities;
using Client.ECS.Components;
using Client.Services;
using Shared;
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

    public void SendInput(InputPacket input)
    {
        if (_server == null) return;
        var writer = new NetDataWriter();
        writer.Put(PacketSerializer.Serialize(input));
        _server.Send(writer, DeliveryMethod.Unreliable);
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
                var join = MessagePackSerializer.Deserialize<JoinAcceptedPacket>(payload);
                _state.LocalId = join.AssignedId;
                var localPlayer = new PlayerEntity(join.AssignedId, true);
                _world.AddEntity(localPlayer);
                _logger.LogInformation("Assigned ID: {Id}", join.AssignedId);
                break;

            case PacketType.WorldStatePacket:
                var ws = MessagePackSerializer.Deserialize<WorldStatePacket>(payload);
                _state.Tick = ws.Tick;
                foreach (var snap in ws.Players)
                {
                    var entity = _world.GetEntitiesWith<NetworkIdComponent>()
                        .FirstOrDefault(e => e.GetComponent<NetworkIdComponent>().Id == snap.Id);

                    if (entity == null)
                    {
                        // Remote player not yet known
                        var remote = new PlayerEntity(snap.Id, snap.Id == _state.LocalId);
                        _world.AddEntity(remote);
                        entity = remote;
                    }

                    var pos = entity.GetComponent<PositionComponent>();
                    pos.X   = snap.X;
                    pos.Y   = snap.Y;
                }
                break;

            case PacketType.PlayerDisconnectedPacket:
                var disc = MessagePackSerializer.Deserialize<PlayerDisconnectedPacket>(payload);
                var toRemove = _world.GetEntitiesWith<NetworkIdComponent>()
                    .FirstOrDefault(e => e.GetComponent<NetworkIdComponent>().Id == disc.Id);
                if (toRemove != null) _world.RemoveEntity(toRemove);
                _logger.LogInformation("Player {Id} left", disc.Id);
                break;
        }
    }

    public void Dispose() => _netManager.Stop();
}
