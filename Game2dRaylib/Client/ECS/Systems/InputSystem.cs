using Client.ECS.Components;
using Client.Network;
using Client.ECS.Systems;
using Raylib_cs;
using Shared.Packets;

namespace Client.ECS.Systems;

public class InputSystem : ISystem
{
    private readonly ClientNetworkManager _network;
    private int _tick;

    public InputSystem(ClientNetworkManager network)
        => _network = network;

    public void Update(float deltaTime)
    {
        var input = new InputPacket
        {
            Up    = Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up),
            Down  = Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down),
            Left  = Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left),
            Right = Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right),
            Tick  = _tick++
        };

        _network.SendInput(input);
    }
}
