using Server.Maps;
using Arch.Core.Extensions;
using LiteNetLib;
using LiteNetLib.Utils;
using Microsoft.Extensions.Logging;
using Server.Events;
using Server.ECS;
using Server.ECS.Components;
using Server.Services;
using Shared;
using Shared.Network;
using Shared.Packets;

namespace Server.Network;

/// <summary>
/// Server-side LiteNetLib network manager.
///
/// Changes vs. original:
/// - Uses PacketSerializer with the new 5-byte framed header.
/// - Maintains a PeerSessionState per connected peer:
///     * Per-peer rate limiting (PeerRateLimiter).
///     * Per-stream sequence number validation for movement packets.
///     * Per-client WorldState delta baseline for delta compression.
/// - Sends WorldDeltaPacket when possible; full WorldStatePacket for new /
///   resync clients.
/// </summary>
public class NetworkManager : IDisposable
{
    private readonly EventBasedNetListener       _listener;
    private readonly NetManager                  _netManager;
    private readonly ILogger<NetworkManager>     _logger;
    private readonly ServerWorld                 _world;
    private readonly PlayerService               _playerService;

    private readonly Dictionary<int, NetPeer>          _peers       = new();
    private readonly Dictionary<int, int>              _peerToNetId = new();
    private readonly Dictionary<int, PeerSessionState> _sessions    = new();

    private int      _nextNetworkId = 1;
    private MapData? _currentMap;

    /// <summary>Call from GameLoop after map is loaded so new peers get the map.</summary>
    public void SetMapData(MapData map) => _currentMap = map;

    private const int MaxPacketsPerSecond = 120;
    private const int AbuseThreshold      = 5;

    private readonly EventBus _eventBus;

    public NetworkManager(ILogger<NetworkManager> logger,
                          ServerWorld world, PlayerService playerService,
                          EventBus eventBus)
    {
        _logger        = logger;
        _world         = world;
        _playerService = playerService;
        _eventBus      = eventBus;
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

    // ── Connection events ─────────────────────────────────────────────────

    private void OnPeerConnected(NetPeer peer)
    {
        int netId = _nextNetworkId++;
        _peers[netId]         = peer;
        _peerToNetId[peer.Id] = netId;
        _sessions[netId]      = new PeerSessionState(netId,
                                                       MaxPacketsPerSecond,
                                                       AbuseThreshold);

        int spawnX = Constants.MapWidth  / 2;
        int spawnY = Constants.MapHeight / 2;
        _world.SpawnPlayer(netId, spawnX, spawnY, Vocation.None);

        _logger.LogInformation("Player {NetId} connected (PeerId={PeerId})", netId, peer.Id);

        var data   = PacketSerializer.Serialize(new JoinAcceptedPacket { AssignedId = netId });
        var writer = new NetDataWriter();
        writer.Put(data);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);

        // Send map data immediately after join so client can render tiles
        if (_currentMap != null)
            SendMapData(peer, _currentMap);
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
        if (!_peerToNetId.TryGetValue(peer.Id, out int netId)) return;
        _peerToNetId.Remove(peer.Id);
        _peers.Remove(netId);
        _sessions.Remove(netId);

        _playerService.RemovePlayer(netId, _world);
        _logger.LogInformation("Player {NetId} disconnected", netId);

        _eventBus.Publish(new PlayerDisconnectedEvent { NetworkId = netId });

        var data = PacketSerializer.Serialize(new PlayerDisconnectedPacket { Id = netId });
        BroadcastReliable(data);
    }

    // ── Receive loop ──────────────────────────────────────────────────────

    private void OnNetworkReceive(NetPeer peer, NetPacketReader reader,
                                  byte channel, DeliveryMethod delivery)
    {
        var data = reader.GetRemainingBytes();
        reader.Recycle();

        if (!_peerToNetId.TryGetValue(peer.Id, out int netId)) return;

        var session = _sessions[netId];

        // 1. Rate limiting
        if (!session.RateLimiter.TryAccept())
        {
            _logger.LogDebug("Rate limit: dropping packet from peer {NetId}", netId);

            if (session.RateLimiter.ShouldDisconnect)
            {
                _logger.LogWarning(
                    "Peer {NetId} exceeded abuse threshold – disconnecting", netId);
                peer.Disconnect();
            }
            return;
        }

        // 2. Parse packet header
        NetworkPacket packet;
        try
        {
            packet = PacketSerializer.ParsePacket(data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Malformed packet from peer {NetId}: {Ex}", netId, ex.Message);
            return;
        }

        // 3. Protocol version check (log mismatch but still process via registry fallback)
        if (packet.ProtocolVersion != NetworkPacket.CurrentVersion)
        {
            _logger.LogWarning(
                "Peer {NetId}: protocol version mismatch (got {Got}, expected {Want})",
                netId, packet.ProtocolVersion, NetworkPacket.CurrentVersion);
        }

        // 4. Dispatch
        switch (packet.Type)
        {
            case PacketType.MoveRequestPacket:
                HandleMoveRequest(packet, netId, session);
                break;

            default:
                _logger.LogDebug("Unhandled packet type {Type} from peer {NetId}",
                                  packet.Type, netId);
                break;
        }
    }

    // ── Packet handlers ───────────────────────────────────────────────────

    private void HandleMoveRequest(NetworkPacket packet, int netId, PeerSessionState session)
    {
        MoveRequestPacket moveReq;
        try
        {
            moveReq = PacketSerializer.ParsePacket<MoveRequestPacket>(packet);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Bad MoveRequest from {NetId}: {Ex}", netId, ex.Message);
            return;
        }

        // Sequence number check – drop duplicates / replays
        if (!session.IsSequenceAcceptable(StreamId.Movement, moveReq.Sequence))
        {
            _logger.LogDebug(
                "Dropping duplicate/old MoveRequest seq={Seq} from peer {NetId}",
                moveReq.Sequence, netId);
            return;
        }

        _playerService.ApplyMoveRequest(netId, moveReq, _world);
    }

    // ── Broadcast helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Broadcasts the current WorldState to all connected peers.
    /// Sends a delta packet where possible; full snapshot for new/desynced clients.
    /// </summary>
    public void BroadcastWorldState(WorldStatePacket packet)
    {
        foreach (var (netId, peer) in _peers)
        {
            if (!_sessions.TryGetValue(netId, out var session)) continue;

            var update = WorldStateDeltaBuilder.BuildUpdate(packet, session);

            byte[] data;
            if (update is WorldDeltaPacket delta)
            {
                data = PacketSerializer.BuildPacket(
                    delta, PacketType.WorldDeltaPacket, compress: true);
            }
            else
            {
                data = PacketSerializer.BuildPacket(
                    (WorldStatePacket)update, PacketType.WorldStatePacket, compress: true);
            }

            var writer = new NetDataWriter();
            writer.Put(data);
            peer.Send(writer, DeliveryMethod.Unreliable);
        }
    }


    /// <summary>
    /// Sends a per-player (AoI-filtered) WorldStatePacket to a single peer.
    /// Uses the same delta-compression pipeline as BroadcastWorldState.
    /// </summary>
    public void SendWorldStateToPlayer(int networkId, WorldStatePacket packet)
    {
        if (!_peers.TryGetValue(networkId, out var peer)) return;
        if (!_sessions.TryGetValue(networkId, out var session)) return;

        var update = WorldStateDeltaBuilder.BuildUpdate(packet, session);

        byte[] data;
        if (update is WorldDeltaPacket delta)
        {
            data = PacketSerializer.BuildPacket(
                delta, PacketType.WorldDeltaPacket, compress: true);
        }
        else
        {
            data = PacketSerializer.BuildPacket(
                (WorldStatePacket)update, PacketType.WorldStatePacket, compress: true);
        }

        var writer = new LiteNetLib.Utils.NetDataWriter();
        writer.Put(data);
        peer.Send(writer, LiteNetLib.DeliveryMethod.Unreliable);
    }
    public void SendStatsToPlayer(int networkId, StatsUpdatePacket packet)
    {
        if (!_peers.TryGetValue(networkId, out var peer)) return;
        var writer = new NetDataWriter();
        writer.Put(PacketSerializer.Serialize(packet));
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    public void SendFloorChange(int networkId, FloorChangePacket packet)
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

    private static void SendMapData(NetPeer peer, MapData map)
    {
        int total = map.Width * map.Height * map.Floors;
        var groundIds = new ushort[total];
        var flags     = new ushort[total];
        for (int z = 0; z < map.Floors;  z++)
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width;  x++)
        {
            int idx = x + y * map.Width + z * map.Width * map.Height;
            groundIds[idx] = map.Tiles[x, y, z].GroundItemId;
            flags[idx]     = (ushort)map.Tiles[x, y, z].Flags;
        }
        var pkt = new MapDataPacket
        {
            Width       = map.Width,
            Height      = map.Height,
            Floors      = map.Floors,
            GroundFloor = map.GroundFloor,
            GroundIds   = groundIds,
            Flags       = flags,
        };
        var data   = PacketSerializer.BuildPacket(pkt, PacketType.MapDataPacket, compress: true);
        var writer = new NetDataWriter();
        writer.Put(data);
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
