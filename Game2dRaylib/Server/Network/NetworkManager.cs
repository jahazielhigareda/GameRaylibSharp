using Arch.Core.Extensions;
using LiteNetLib;
using LiteNetLib.Utils;
using MessagePack;
using Microsoft.Extensions.Logging;
using Server.ECS;
using Server.ECS.Components;
using Server.Services;
using Shared;
using Shared.Network;
using Shared.Packets;

namespace Server.Network;

public class NetworkManager : IDisposable
{
    private readonly EventBasedNetListener       _listener;
    private readonly NetManager                  _netManager;
    private readonly ILogger<NetworkManager>     _logger;
    private readonly ServerWorld                 _world;
    private readonly PlayerService               _playerService;

    private readonly Dictionary<int, NetPeer>   _peers       = new();
    private readonly Dictionary<int, int>       _peerToNetId = new();
    private int _nextNetworkId = 1;

    public NetworkManager(ILogger<NetworkManager> logger,
                          ServerWorld world, PlayerService playerService)
    {
        _logger        = logger;
        _world         = world;
        _playerService = playerService;
        _listener      = new EventBasedNetListener();
        _netManager    = new NetManager(_listener);

        _listener.ConnectionRequestEvent += r => r.Accept();
        _listener.PeerConnectedEvent     += OnPeerConnected;
        _listener.PeerDisconnectedEvent  += OnPeerDisconnected;
        _listener.NetworkReceiveEvent    += OnNetworkReceive;
    }

    public void Start()
    {
        _netManager.Start(Constants.ServerPort);
        _logger.LogInformation("Server listening on port {Port}", Constants.ServerPort);
    }

    public void PollEvents() => _netManager.PollEvents();

    private void OnPeerConnected(NetPeer peer)
    {
        int netId = _nextNetworkId++;
        _peers[netId]         = peer;
        _peerToNetId[peer.Id] = netId;

        int spawnX = Constants.MapWidth / 2;
        int spawnY = Constants.MapHeight / 2;
        _world.SpawnPlayer(netId, spawnX, spawnY, Vocation.None);

        _logger.LogInformation("Player {NetId} connected (PeerId={PeerId})", netId, peer.Id);

        var accepted = PacketSerializer.Serialize(new JoinAcceptedPacket { AssignedId = netId });
        var writer   = new NetDataWriter();
        writer.Put(accepted);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
        if (!_peerToNetId.TryGetValue(peer.Id, out int netId)) return;
        _peerToNetId.Remove(peer.Id);
        _peers.Remove(netId);
        _playerService.RemovePlayer(netId, _world);
        _logger.LogInformation("Player {NetId} disconnected", netId);
        var pkt = PacketSerializer.Serialize(new PlayerDisconnectedPacket { Id = netId });
        BroadcastReliable(pkt);
    }

    private void OnNetworkReceive(NetPeer peer, NetPacketReader reader,
                                  byte channel, DeliveryMethod delivery)
    {
        var data = reader.GetRemainingBytes();
        reader.Recycle();
        var (type, payload) = PacketSerializer.Deserialize(data);

        switch (type)
        {
            case PacketType.MoveRequestPacket:
                if (!_peerToNetId.TryGetValue(peer.Id, out int netId)) return;
                var moveReq = MessagePackSerializer.Deserialize<MoveRequestPacket>(payload);
                _playerService.ApplyMoveRequest(netId, moveReq, _world);
                break;
        }
    }

    public void BroadcastWorldState(WorldStatePacket packet)
    {
        var data = PacketSerializer.Serialize(packet);
        foreach (var peer in _peers.Values)
        {
            var writer = new NetDataWriter();
            writer.Put(data);
            peer.Send(writer, DeliveryMethod.Unreliable);
        }
    }

    public void SendStatsToPlayer(int networkId, StatsUpdatePacket packet)
    {
        if (!_peers.TryGetValue(networkId, out var peer)) return;
        var writer = new NetDataWriter();
        writer.Put(PacketSerializer.Serialize(packet));
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    public void SendSkillsToPlayer(int networkId, SkillsUpdatePacket packet)
    {
        if (!_peers.TryGetValue(networkId, out var peer)) return;
        var writer = new NetDataWriter();
        writer.Put(PacketSerializer.Serialize(packet));
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void BroadcastReliable(byte[] data)
    {
        foreach (var peer in _peers.Values)
        {
            var writer = new NetDataWriter();
            writer.Put(data);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }

    public void Dispose() => _netManager.Stop();
}
