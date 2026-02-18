using Client.Network;
using Client.ECS.Systems;
using Raylib_cs;
using Shared;
using Shared.Packets;

namespace Client.ECS.Systems;

/// <summary>
/// Sistema de input estilo Tibia:
/// - Detecta teclas de dirección (WASD + flechas)
/// - Envía MoveRequestPacket al servidor cuando se presiona una dirección
/// - Soporta 8 direcciones (cardinal + diagonal)
/// - Implementa key repeat: al mantener presionada, re-envía periódicamente
/// </summary>
public class InputSystem : ISystem
{
    private readonly ClientNetworkManager _network;
    private int _tick;

    // Para key repeat
    private Direction _lastSentDirection = Direction.None;
    private float     _repeatTimer;
    private const float RepeatInterval = 0.08f; // Re-enviar cada 80ms al mantener presionado

    public InputSystem(ClientNetworkManager network)
        => _network = network;

    public void Update(float deltaTime)
    {
        var dir = GetCurrentDirection();

        if (dir != Direction.None)
        {
            // Si cambió la dirección o pasó el intervalo de repeat
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
            // No hay tecla presionada
            if (_lastSentDirection != Direction.None)
            {
                // Enviar "stop" (Direction.None)
                SendMoveRequest(Direction.None);
                _lastSentDirection = Direction.None;
            }
            _repeatTimer = 0f;
        }
    }

    private Direction GetCurrentDirection()
    {
        bool up    = Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up);
        bool down  = Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down);
        bool left  = Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left);
        bool right = Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right);

        // Cancelar opuestos
        if (up && down)    { up = false; down = false; }
        if (left && right) { left = false; right = false; }

        if (up && right)   return Direction.NorthEast;
        if (up && left)    return Direction.NorthWest;
        if (down && right) return Direction.SouthEast;
        if (down && left)  return Direction.SouthWest;
        if (up)            return Direction.North;
        if (down)          return Direction.South;
        if (left)          return Direction.West;
        if (right)         return Direction.East;

        return Direction.None;
    }

    private void SendMoveRequest(Direction dir)
    {
        var packet = new MoveRequestPacket
        {
            Direction = (byte)dir,
            Tick      = _tick++
        };
        _network.SendMoveRequest(packet);
    }
}
