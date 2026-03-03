using Client.ECS;
using Client.Services;
using Client.ECS.Systems;
using Client.Network;
using Raylib_cs;
using Shared;

namespace Client.Core;

public class GameLoop
{
    private readonly ClientWorld          _world;
    private readonly ClientNetworkManager _network;
    private readonly InputSystem          _inputSystem;
    private readonly InterpolationSystem  _interpolationSystem;
    private readonly AnimationSystem      _animationSystem;
    private readonly TileRenderSystem     _tileRenderSystem;
    private readonly CreatureRenderSystem _creatureRenderSystem;
    private readonly EffectRenderSystem   _effectRenderSystem;
    private readonly RenderSystem         _renderSystem;
    private readonly HudSystem            _hudSystem;
    private readonly BackgroundSystem     _backgroundSystem;
    private readonly SpriteService        _spriteService;

    public GameLoop(
        ClientWorld world,
        ClientNetworkManager network,
        InputSystem inputSystem,
        InterpolationSystem interpolationSystem,
        AnimationSystem animationSystem,
        TileRenderSystem tileRenderSystem,
        CreatureRenderSystem creatureRenderSystem,
        EffectRenderSystem effectRenderSystem,
        RenderSystem renderSystem,
        HudSystem hudSystem,
        BackgroundSystem backgroundSystem,
        SpriteService spriteService)
    {
        _world                = world;
        _network              = network;
        _inputSystem          = inputSystem;
        _interpolationSystem  = interpolationSystem;
        _animationSystem      = animationSystem;
        _tileRenderSystem     = tileRenderSystem;
        _creatureRenderSystem = creatureRenderSystem;
        _effectRenderSystem   = effectRenderSystem;
        _renderSystem         = renderSystem;
        _hudSystem            = hudSystem;
        _backgroundSystem     = backgroundSystem;
        _spriteService        = spriteService;
    }

    public void Run()
    {
        Raylib.InitWindow(800, 600, "Game2dRaylib - Tibia Movement [Arch ECS]");
        Raylib.SetTargetFPS(Constants.TickRate);

        // Sprite atlas must be generated after the window (OpenGL context) is ready
        _spriteService.Initialize();

        _network.SetTileRenderSystem(_tileRenderSystem);
        _network.Connect();

        while (!Raylib.WindowShouldClose())
        {
            float dt = Raylib.GetFrameTime();

            _network.PollEvents();
            _inputSystem.Update(dt);
            _interpolationSystem.Update(dt);
            _animationSystem.Update(dt);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            // Layer 0-2,4-5: tiles (ground, borders, bottom/top items, effects)
            _tileRenderSystem.Update(dt);

            // Layer 3: creatures + players (Y-sorted painter's algorithm)
            _creatureRenderSystem.Update(dt);

            // Layer 5: transient spell/particle effects
            _effectRenderSystem.Update(dt);

            // Legacy entity render (kept until full migration)
            _renderSystem.Update(dt);

            _hudSystem.Update(dt);

            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
        _spriteService.Dispose();
        _world.Dispose();
    }
}
