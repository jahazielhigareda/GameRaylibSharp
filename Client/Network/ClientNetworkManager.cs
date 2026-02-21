using Arch.Core.Extensions;
using LiteNetLib;
using LiteNetLib.Utils;
using Microsoft.Extensions.Logging;
using Client.ECS;
using Client.ECS.Components;
using Client.ECS.Systems;
using Client.Services;
using Shared;
using Shared.Network;
using Shared.Packets;

namespace Client.Network;

public class ClientNetworkManager : IDisposable
{
    private readonly EventBasedNetListener         _listener;
    private readonly NetManager                    _netManager;
    private readonly ILogger<ClientNetworkManager> _logger;
    private readonly ClientWorld                   _world;
    private readonly GameStateService              _state;
    private NetPeer? _server;
    private TileRenderSystem? _tileRenderSystem;
    private TileCell[,,]?     _pendingGrid;        // cache si TRS no est� listo
    private byte              _pendingGroundFloor;

    private ushort _moveSequence;

    public ClientNetworkManager(ILogger<ClientNetworkManager> logger,
                                ClientWorld world, GameStateService state)
    {
        _logger     = logger;
        _world      = world;
        _state      = state;
        _listener   = new EventBasedNetListener();
        _netManager = new NetManager(_listener);

        _listener.PeerConnectedEvent    += _ => _logger.LogInformation("Connected to server");
        _listener.PeerDisconnectedEvent += (_, i) => _logger.LogWarning("Disconnected: {R}", i.Reason);
        _listener.NetworkReceiveEvent   += OnReceive;
    }

    public void SetTileRenderSystem(TileRenderSystem trs)
    {
        _tileRenderSystem = trs;
        if (_pendingGrid != null)
        {
            _tileRenderSystem.SetMapGrid3D(_pendingGrid);
            _state.CurrentFloorZ = _pendingGroundFloor;
            _logger.LogInformation("Pending map applied: floor={F}", _pendingGroundFloor);
            _pendingGrid = null;
        }
    }

    public void Connect()
    {
        _netManager.Start();
        _server = _netManager.Connect(Constants.ServerAddress, Constants.ServerPort, "");
        _logger.LogInformation("Connecting to {Address}:{Port}",
            Constants.ServerAddress, Constants.ServerPort);
    }

    public void PollEvents() => _netManager.PollEvents();

    public void SendMoveRequest(MoveRequestPacket packet)
    {
        if (_server == null) return;
        packet.Sequence = _moveSequence++;
        var data   = PacketSerializer.Serialize(packet);
        var writer = new NetDataWriter();
        writer.Put(data);
        _server.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    public void SendTargetRequest(int creatureNetId)
    {
        var data = PacketSerializer.Serialize(
            new TargetRequestPacket { CreatureNetId = creatureNetId });
        var writer = new LiteNetLib.Utils.NetDataWriter();
        writer.Put(data);
        _server?.Send(writer, LiteNetLib.DeliveryMethod.ReliableOrdered);
    }

    private void OnReceive(NetPeer peer, NetPacketReader reader,
                           byte channel, DeliveryMethod delivery)
    {
        var data = reader.GetRemainingBytes();
        reader.Recycle();

        NetworkPacket packet;
        try
        {
            packet = PacketSerializer.ParsePacket(data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Malformed packet from server: {Ex}", ex.Message);
            return;
        }

        switch (packet.Type)
        {
            case PacketType.JoinAcceptedPacket:       HandleJoinAccepted(packet);       break;
            case PacketType.WorldStatePacket:         HandleWorldStateFull(packet);     break;
            case PacketType.WorldDeltaPacket:         HandleWorldStateDelta(packet);    break;
            case PacketType.PlayerDisconnectedPacket: HandlePlayerDisconnected(packet); break;
            case PacketType.StatsUpdatePacket:        HandleStatsUpdate(packet);        break;
            case PacketType.SkillsUpdatePacket:       HandleSkillsUpdate(packet);       break;
            case PacketType.MapDataPacket:            HandleMapData(packet);            break;
            case PacketType.FloorChangePacket:        HandleFloorChange(packet);        break;
        }
    }

    private void HandleJoinAccepted(NetworkPacket packet)
    {
        var join = PacketSerializer.ParsePacket<JoinAcceptedPacket>(packet);
        _state.LocalId = join.AssignedId;
        _world.SpawnPlayer(join.AssignedId, isLocal: true);
        _logger.LogInformation("Assigned ID: {Id}", join.AssignedId);
    }

    private void HandleWorldStateFull(NetworkPacket packet)
    {
        var ws = PacketSerializer.ParsePacket<WorldStatePacket>(packet);
        _state.Tick = ws.Tick;
        ApplyFullSnapshot(ws.Players);
    }

    private void HandleWorldStateDelta(NetworkPacket packet)
    {
        var delta = PacketSerializer.ParsePacket<WorldDeltaPacket>(packet);
        _state.Tick = delta.Tick;

        if (delta.Added.Count > 0)
            ApplyFullSnapshot(delta.Added);

        foreach (var d in delta.Updated)
        {
            // Try player first, then creature
            var entity = _world.FindPlayer(d.Id);
            if (entity == Arch.Core.Entity.Null)
                entity = _world.FindCreature(d.Id);
            if (entity == Arch.Core.Entity.Null) continue;

            // Update HP (applies to creatures when they take damage)
            if (d.HpPct.HasValue && entity.Has<CreatureHpComponent>())
                entity.Get<CreatureHpComponent>().HpPct = d.HpPct.Value;

            // Update position
            if (entity.Has<PositionComponent>())
            {
                ref var pos = ref entity.Get<PositionComponent>();
                if (d.TileX.HasValue) pos.TileX   = d.TileX.Value;
                if (d.TileY.HasValue) pos.TileY   = d.TileY.Value;
                if (d.X.HasValue)     pos.TargetX = d.X.Value;
                if (d.Y.HasValue)     pos.TargetY = d.Y.Value;
            }
        }

        foreach (var id in delta.Removed)
        {
            var entity = _world.FindPlayer(id);
            if (entity != Arch.Core.Entity.Null) _world.DestroyEntity(entity);
        }
    }

    private void ApplyFullSnapshot(List<PlayerSnapshot> players)
    {
        foreach (var snap in players)
        {
            // Route creatures to UpsertCreature (not SpawnPlayer)
            if (snap.EntityType == Shared.Packets.SnapshotEntityType.Creature)
            {
                _world.UpsertCreature(snap.Id, snap.TileX, snap.TileY,
                                      snap.X, snap.Y, snap.HpPct,
                                      snap.Name ?? string.Empty);
                continue;
            }
            var entity = _world.FindPlayer(snap.Id);
            if (entity == Arch.Core.Entity.Null)
            {
                entity = _world.SpawnPlayer(snap.Id, snap.Id == _state.LocalId);
                ref var newPos = ref entity.Get<PositionComponent>();
                newPos.SetFromServer(snap.TileX, snap.TileY, snap.X, snap.Y);
                newPos.SnapToTarget();
                continue;
            }
            ref var pos = ref entity.Get<PositionComponent>();
            pos.SetFromServer(snap.TileX, snap.TileY, snap.X, snap.Y);
        }
    }

    private void HandlePlayerDisconnected(NetworkPacket packet)
    {
        var disc   = PacketSerializer.ParsePacket<PlayerDisconnectedPacket>(packet);
        var entity = _world.FindPlayer(disc.Id);
        if (entity != Arch.Core.Entity.Null) _world.DestroyEntity(entity);
        _logger.LogInformation("Player {Id} left", disc.Id);
    }

    private void HandleStatsUpdate(NetworkPacket packet)
    {
        var stats = PacketSerializer.ParsePacket<StatsUpdatePacket>(packet);
        if (stats.PlayerId != _state.LocalId) return;
        if (!_world.TryGetLocalPlayer(out var local)) return;
        if (!local.Has<StatsDataComponent>()) return;
        ref var data = ref local.Get<StatsDataComponent>();
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

    private void HandleSkillsUpdate(NetworkPacket packet)
    {
        var skills = PacketSerializer.ParsePacket<SkillsUpdatePacket>(packet);
        if (skills.PlayerId != _state.LocalId) return;
        if (!_world.TryGetLocalPlayer(out var local)) return;
        if (!local.Has<SkillsDataComponent>()) return;
        ref var data = ref local.Get<SkillsDataComponent>();
        data.FistLevel        = skills.FistLevel;
        data.FistPercent      = skills.FistPercent;
        data.ClubLevel        = skills.ClubLevel;
        data.ClubPercent      = skills.ClubPercent;
        data.SwordLevel       = skills.SwordLevel;
        data.SwordPercent     = skills.SwordPercent;
        data.AxeLevel         = skills.AxeLevel;
        data.AxePercent       = skills.AxePercent;
        data.DistanceLevel    = skills.DistanceLevel;
        data.DistancePercent  = skills.DistancePercent;
        data.ShieldingLevel   = skills.ShieldingLevel;
        data.ShieldingPercent = skills.ShieldingPercent;
        data.FishingLevel     = skills.FishingLevel;
        data.FishingPercent   = skills.FishingPercent;
        data.MagicLevel       = skills.MagicLevel;
        data.MagicPercent     = skills.MagicPercent;
    }

    private void HandleMapData(NetworkPacket packet)
    {
        var mp = PacketSerializer.ParsePacket<MapDataPacket>(packet);
        _logger.LogInformation("Map received: {W}x{H}x{F}", mp.Width, mp.Height, mp.Floors);

        if (_tileRenderSystem == null)
        {
            _logger.LogWarning("HandleMapData: _tileRenderSystem is null – map discarded!");
            return;
        }

        var grid = new TileCell[mp.Width, mp.Height, mp.Floors];
        for (int z = 0; z < mp.Floors;  z++)
        for (int y = 0; y < mp.Height; y++)
        for (int x = 0; x < mp.Width;  x++)
        {
            int idx = x + y * mp.Width + z * mp.Width * mp.Height;
            grid[x, y, z] = TileCell.FromGroundId(mp.GroundIds[idx], mp.Flags[idx]);
        }
        _tileRenderSystem.SetMapGrid3D(grid);
        _state.CurrentFloorZ = mp.GroundFloor;
        _logger.LogInformation("Map grid set. Floor={F}", mp.GroundFloor);
    }

    private void HandleFloorChange(NetworkPacket packet)
    {
        var fc = PacketSerializer.ParsePacket<FloorChangePacket>(packet);
        _state.CurrentFloorZ = fc.ToZ;
        if (_world.TryGetLocalPlayer(out var local))
        {
            ref var pos = ref local.Get<PositionComponent>();
            pos.TileX = fc.X;
            pos.TileY = fc.Y;
        }
        _logger.LogInformation("Floor changed: {From} -> {To}", fc.FromZ, fc.ToZ);
    }

    public void Dispose() => _netManager.Stop();
}
