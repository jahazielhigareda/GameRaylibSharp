using Client.Network;
using Raylib_cs;
using Shared;
using Shared.Packets;

namespace Client.ECS.Systems;

/// <summary>
/// Tibia-style input system – 8-directional, key-repeat, sends MoveRequestPacket.
/// The MoveRequestPacket.Sequence field is stamped by
/// ClientNetworkManager.SendMoveRequest, so this system does not manage it.
/// </summary>
public class InputSystem : ISystem
{
    private readonly ClientNetworkManager _network;
    private int       _tick;
    private Direction _lastSentDirection = Direction.None;
    private float     _repeatTimer;
    private const float RepeatInterval = 0.08f;

    public InputSystem(ClientNetworkManager network) => _network = network;

    public void Update(float deltaTime)
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
            // Sequence is stamped inside ClientNetworkManager.SendMoveRequest
        });
}
