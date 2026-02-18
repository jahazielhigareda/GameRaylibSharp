using Client.ECS.Components;
using Client.Services;
using Raylib_cs;

namespace Client.ECS.Systems;

public class HudSystem : ISystem
{
    private readonly World            _world;
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

        Raylib.DrawText($"Players: {players}",         10, 10, 20, Color.White);
        Raylib.DrawText($"Server tick: {_state.Tick}",  10, 35, 20, Color.White);
        Raylib.DrawText($"Local ID: {_state.LocalId}",  10, 60, 20, Color.White);
        Raylib.DrawText($"FPS: {fps}",                  10, 85, 20, Color.White);

        // Mostrar posición del jugador local
        var local = _world.GetEntitiesWith<LocalPlayerComponent>().FirstOrDefault();
        if (local != null)
        {
            var pos = local.GetComponent<PositionComponent>();
            Raylib.DrawText($"Tile: ({pos.TileX}, {pos.TileY})", 10, 110, 20, Color.Yellow);
        }

        Raylib.DrawText("Movimiento estilo Tibia (WASD/Flechas)", 10,
            Raylib.GetScreenHeight() - 30, 18, Color.LightGray);
    }
}
