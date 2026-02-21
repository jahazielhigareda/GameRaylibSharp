using Arch.Core;
using Arch.Core.Extensions;
using Client.ECS;
using Client.ECS.Components;
using Client.Network;
using Client.Services;
using Raylib_cs;
using Shared;
using Shared.Packets;

namespace Client.ECS.Systems;

/// <summary>
/// Tibia-style input: 8-directional movement + click-to-target.
///
/// Left-click  on a creature tile → send TargetRequestPacket
/// Right-click anywhere           → clear current target
/// WASD / Arrow keys              → movement
///
/// Click detection uses the full TILE (TileSize x TileSize) rather than
/// the small sprite rectangle. TileX/TileY are always correct even before
/// visual interpolation catches up, because they are set directly from the
/// server snapshot.
/// </summary>
public class InputSystem : ISystem
{
    private readonly ClientNetworkManager _network;
    private readonly ClientWorld          _world;
    private readonly CameraService        _camera;
    private readonly GameStateService     _state;

    private Direction _lastSentDirection = Direction.None;
    private float     _repeatTimer;
    private const float RepeatInterval = 0.08f;

    private static readonly QueryDescription CreatureQuery = new QueryDescription()
        .WithAll<NetworkIdComponent, PositionComponent, CreatureClientTag>();

    public InputSystem(ClientNetworkManager network, ClientWorld world,
                       CameraService camera, GameStateService state)
    {
        _network = network;
        _world   = world;
        _camera  = camera;
        _state   = state;
    }

    public void Update(float deltaTime)
    {
        HandleClickTargeting();
        HandleMovement(deltaTime);
    }

    // ── Click-to-target ───────────────────────────────────────────────────

    private void HandleClickTargeting()
    {
        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            _state.TargetedEntityId = 0;
            _network.SendTargetRequest(0);
            return;
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;

        var (offsetX, offsetY) = _camera.GetOffset();
        var mousePos           = Raylib.GetMousePosition();
        int ts                 = Constants.TileSize;

        int   clickedNetId = 0;
        float bestDist     = float.MaxValue;

        _world.World.Query(in CreatureQuery,
            (Entity e, ref NetworkIdComponent nid, ref PositionComponent pos) =>
            {
                // Use tile screen position — always valid regardless of interpolation
                float tileScreenX = pos.TileX * ts + offsetX;
                float tileScreenY = pos.TileY * ts + offsetY;

                var tileBounds = new Rectangle(tileScreenX, tileScreenY, ts, ts);
                if (!Raylib.CheckCollisionPointRec(mousePos, tileBounds)) return;

                // Prefer closest to mouse centre when multiple overlap
                float cx   = tileScreenX + ts * 0.5f;
                float cy   = tileScreenY + ts * 0.5f;
                float dist = MathF.Sqrt(
                    (mousePos.X - cx) * (mousePos.X - cx) +
                    (mousePos.Y - cy) * (mousePos.Y - cy));

                if (dist < bestDist)
                {
                    bestDist     = dist;
                    clickedNetId = nid.Id;
                }
            });

        if (clickedNetId != 0)
        {
            _state.TargetedEntityId = clickedNetId;
            _network.SendTargetRequest(clickedNetId);
        }
    }

    // ── Movement ──────────────────────────────────────────────────────────

    private void HandleMovement(float deltaTime)
    {
        var dir = GetCurrentDirection();

        if (dir != Direction.None)
        {
            if (dir != _lastSentDirection || _repeatTimer <= 0f)
            {
                SendMoveRequest(dir);
                _repeatTimer = RepeatInterval;
            }
            else
            {
                _repeatTimer -= deltaTime;
            }
            _lastSentDirection = dir;
        }
        else
        {
            _lastSentDirection = Direction.None;
            _repeatTimer       = 0f;
        }
    }

    private static Direction GetCurrentDirection()
    {
        if (Raylib.IsKeyDown(KeyboardKey.Up)    || Raylib.IsKeyDown(KeyboardKey.W)) return Direction.North;
        if (Raylib.IsKeyDown(KeyboardKey.Down)  || Raylib.IsKeyDown(KeyboardKey.S)) return Direction.South;
        if (Raylib.IsKeyDown(KeyboardKey.Left)  || Raylib.IsKeyDown(KeyboardKey.A)) return Direction.West;
        if (Raylib.IsKeyDown(KeyboardKey.Right) || Raylib.IsKeyDown(KeyboardKey.D)) return Direction.East;
        return Direction.None;
    }

    private void SendMoveRequest(Direction dir)
    {
        _network.SendMoveRequest(new MoveRequestPacket { Direction = (byte)dir });
    }
}
