using Arch.Core.Extensions;
using Client.ECS.Components;
using Client.Services;
using Raylib_cs;

namespace Client.ECS.Systems;

/// <summary>
/// Arch-based HUD system. Renders the permanent debug overlay and bottom HP/MP bars.
/// Stats and Skills panels are now handled by <see cref="GuiSystem"/> as draggable windows.
/// </summary>
public class HudSystem : ISystem
{
    private readonly ClientWorld      _world;
    private readonly GameStateService _state;

    public HudSystem(ClientWorld world, GameStateService state)
    {
        _world = world;
        _state = state;
    }

    public void Update(float deltaTime)
    {
        int players = _world.CountPlayers();
        int fps     = Raylib.GetFPS();
        int sw      = Raylib.GetScreenWidth();
        int sh      = Raylib.GetScreenHeight();

        Raylib.DrawText($"Players: {players}",         10, 10, 20, Color.White);
        Raylib.DrawText($"Server tick: {_state.Tick}", 10, 35, 20, Color.White);
        Raylib.DrawText($"FPS: {fps}",                 10, 60, 20, Color.White);

        if (!_world.TryGetLocalPlayer(out var local)) return;

        ref var pos = ref local.Get<PositionComponent>();
        Raylib.DrawText($"Tile: ({pos.TileX}, {pos.TileY})", 10, 85, 20, Color.Yellow);

        if (local.Has<StatsDataComponent>())
        {
            ref var stats = ref local.Get<StatsDataComponent>();
            DrawHealthBars(in stats, sw, sh);
        }

        // Target indicator
        if (_state.TargetedEntityId != 0)
        {
            Raylib.DrawText($"Target ID: {_state.TargetedEntityId}", 10, sh - 55, 16, Raylib_cs.Color.Red);
        }

        Raylib.DrawText("WASD/Arrows: Move | K: Skills | M: Minimap | LClick: Target | RClick: Clear target", 10, sh - 30, 16, Color.LightGray);
    }

    private static void DrawHealthBars(in StatsDataComponent stats, int sw, int sh)
    {
        int barW = 200, barH = 20;
        int barX = (sw - barW) / 2;
        int hpY  = sh - 60, mpY = sh - 35;

        float hpRatio = stats.MaxHP > 0 ? (float)stats.CurrentHP / stats.MaxHP : 0;
        Raylib.DrawRectangle(barX, hpY, barW, barH, new Color(40,0,0,200));
        Raylib.DrawRectangle(barX, hpY, (int)(barW * hpRatio), barH, new Color(200,30,30,255));
        Raylib.DrawRectangleLines(barX, hpY, barW, barH, new Color(150,50,50,255));
        string hpText = $"{stats.CurrentHP}/{stats.MaxHP}";
        Raylib.DrawText(hpText, barX + barW/2 - Raylib.MeasureText(hpText,14)/2, hpY+3, 14, Color.White);

        float mpRatio = stats.MaxMP > 0 ? (float)stats.CurrentMP / stats.MaxMP : 0;
        Raylib.DrawRectangle(barX, mpY, barW, barH, new Color(0,0,40,200));
        Raylib.DrawRectangle(barX, mpY, (int)(barW * mpRatio), barH, new Color(30,80,200,255));
        Raylib.DrawRectangleLines(barX, mpY, barW, barH, new Color(50,50,150,255));
        string mpText = $"{stats.CurrentMP}/{stats.MaxMP}";
        Raylib.DrawText(mpText, barX + barW/2 - Raylib.MeasureText(mpText,14)/2, mpY+3, 14, Color.White);
    }
}

