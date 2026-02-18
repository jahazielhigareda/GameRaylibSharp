using Client.ECS;
using Client.ECS.Systems;
using Client.Network;
using Raylib_cs;
using Shared;

namespace Client.Core;

public class GameLoop
{
    private readonly World                  _world;
    private readonly ClientNetworkManager   _network;
    private readonly InputSystem            _inputSystem;
    private readonly InterpolationSystem    _interpolationSystem;
    private readonly RenderSystem           _renderSystem;
    private readonly HudSystem              _hudSystem;
    private readonly BackgroundSystem       _backgroundSystem;

    public GameLoop(
        World world,
        ClientNetworkManager network,
        InputSystem inputSystem,
        InterpolationSystem interpolationSystem,
        RenderSystem renderSystem,
        HudSystem hudSystem,
        BackgroundSystem backgroundSystem)
    {
        _world               = world;
        _network             = network;
        _inputSystem         = inputSystem;
        _interpolationSystem = interpolationSystem;
        _renderSystem        = renderSystem;
        _hudSystem           = hudSystem;
        _backgroundSystem    = backgroundSystem;
    }

    public void Run()
    {
        Raylib.InitWindow(800, 600, "Game2dRaylib - Tibia Movement");
        Raylib.SetTargetFPS(Constants.TickRate);

        _network.Connect();

        while (!Raylib.WindowShouldClose())
        {
            float dt = Raylib.GetFrameTime();

            _network.PollEvents();
            _inputSystem.Update(dt);
            _interpolationSystem.Update(dt);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.DarkGray);

            _backgroundSystem.Update(dt);
            _renderSystem.Update(dt);
            _hudSystem.Update(dt);

            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }
}
