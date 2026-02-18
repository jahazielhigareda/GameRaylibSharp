using Client.ECS.Components;
using Client.ECS.Systems;
using Client.Services;
using Raylib_cs;

namespace Client.ECS.Systems;

public class HudSystem : ISystem
{
    private readonly World         _world;
    private readonly GameStateService _state;

    public HudSystem(World world, GameStateService state)
    {
        _world = world;
        _state = state;
    }

    public void Update(float deltaTime)
    {
        int players = _world.GetEntitiesWith<NetworkIdComponent>().Count();
        int fps     = Raylib.GetFPS();

        Raylib.DrawText($"Players : {players}",        10, 10, 20, Color.White);
        Raylib.DrawText($"Server tick: {_state.Tick}", 10, 35, 20, Color.White);
        Raylib.DrawText($"Local ID : {_state.LocalId}", 10, 60, 20, Color.White);
        Raylib.DrawText($"FPS : {fps}",                10, 85, 20, Color.White);
    }
}
