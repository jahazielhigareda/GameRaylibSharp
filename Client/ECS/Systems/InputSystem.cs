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
/// Tibia-style input system – 8-directional movement + click-to-target.
///
/// Left-click  on a creature  → send TargetRequestPacket (target it)
/// Right-click anywhere       → clear current target
/// WASD/Arrows                → movement (MoveRequestPacket)
/// </summary>
public class InputSystem : ISystem
{
    private readonly ClientNetworkManager _network;
    private readonly ClientWorld          _world;
    private readonly CameraService        _camera;
    private readonly GameStateService     _state;

    private int   _tick;
    private Direction _lastSentDirection = Direction.None;
    private float _repeatTimer;
    private const float RepeatInterval = 0.08f;

    // Arch query to find creature entities
    private static readonly QueryDescription CreatureQuery = new QueryDescription()
        .WithAll<NetworkIdComponent, PositionComponent, RenderComponent, CreatureClientTag>();

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
        // Right-click clears target
        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            _state.TargetedEntityId = 0;
            _network.SendTargetRequest(0);
            return;
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;

        var (offsetX, offsetY) = _camera.GetOffset();
        var mousePos = Raylib.GetMousePosition();
        int ts = Constants.TileSize;

        int clickedNetId = 0;

        _world.World.Query(in CreatureQuery,
            (Entity e, ref NetworkIdComponent nid, ref PositionComponent pos, ref RenderComponent render) =>
            {
                // Already found one this frame
                if (clickedNetId != 0) return;

                float drawX = pos.X + offsetX + (ts - render.Size) / 2f;
                float drawY = pos.Y + offsetY + (ts - render.Size) / 2f;

                var bounds = new Rectangle(drawX, drawY, render.Size, render.Size);
                if (Raylib.CheckCollisionPointRec(mousePos, bounds))
                    clickedNetId = nid.Id;
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
            else _repeatTimer -= deltaTime;
            _lastSentDirection = dir;
        }
        else
        {
            if (_lastSentDirection != Direction.None)
            {
                SendMoveRequest(Direction.None);
                _lastSentDirection = Direction.None;
            }
            _repeatTimer = 0f;
        }
    }

    private static Direction GetCurrentDirection()
    {
        bool up    = Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up);
        bool down  = Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down);
        bool left  = Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left);
        bool right = Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right);

        if (up   && down)  { up   = down  = false; }
        if (left && right) { left = right = false; }

        if (up   && right) return Direction.NorthEast;
        if (up   && left)  return Direction.NorthWest;
        if (down && right) return Direction.SouthEast;
        if (down && left)  return Direction.SouthWest;
        if (up)            return Direction.North;
        if (down)          return Direction.South;
        if (left)          return Direction.West;
        if (right)         return Direction.East;
        return Direction.None;
    }

    private void SendMoveRequest(Direction dir)
        => _network.SendMoveRequest(new MoveRequestPacket
        {
            Direction = (byte)dir,
            Tick      = _tick++
        });
}
