using Arch.Core.Extensions;
using Client.ECS.Components;
using Client.Services;
using Raylib_cs;

namespace Client.ECS.Systems;

/// <summary>
/// Arch-based HUD system. Reads stats/skills from local player entity.
/// </summary>
public class HudSystem : ISystem
{
    private readonly ClientWorld      _world;
    private readonly GameStateService _state;
    private bool _showSkills;

    public HudSystem(ClientWorld world, GameStateService state)
    {
        _world = world;
        _state = state;
    }

    public void Update(float deltaTime)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.K))
            _showSkills = !_showSkills;

        int players = _world.CountPlayers();
        int fps     = Raylib.GetFPS();
        int sw      = Raylib.GetScreenWidth();
        int sh      = Raylib.GetScreenHeight();

        Raylib.DrawText($"Online: {players}",         10, 10, 20, Color.White);
        Raylib.DrawText($"Server tick: {_state.Tick}", 10, 35, 20, Color.White);
        Raylib.DrawText($"FPS: {fps}",                 10, 60, 20, Color.White);

        if (!_world.TryGetLocalPlayer(out var local)) return;

        ref var pos = ref local.Get<PositionComponent>();
        Raylib.DrawText($"Tile: ({pos.TileX}, {pos.TileY})", 10, 85, 20, Color.Yellow);

        if (local.Has<StatsDataComponent>())
        {
            ref var stats = ref local.Get<StatsDataComponent>();
            DrawStatsPanel(in stats, sw, sh);
            DrawHealthBars(in stats, sw, sh);
        }

        if (_showSkills && local.Has<SkillsDataComponent>())
        {
            ref var skills = ref local.Get<SkillsDataComponent>();
            DrawSkillsPanel(in skills, sw, sh);
        }

        // Target indicator
        if (_state.TargetedEntityId != 0)
        {
            Raylib.DrawText($"Target ID: {_state.TargetedEntityId}", 10, sh - 55, 16, Raylib_cs.Color.Red);
        }

        Raylib.DrawText("WASD/Arrows: Move | K: Skills | LClick: Target | RClick: Clear target", 10, sh - 30, 16, Color.LightGray);
    }

    private static void DrawStatsPanel(in StatsDataComponent stats, int sw, int sh)
    {
        int panelW = 200, panelX = sw - 210, panelY = 10, lineH = 22;
        Raylib.DrawRectangle(panelX - 5, panelY - 5, panelW + 10, lineH * 10 + 10, new Color(0,0,0,180));
        Raylib.DrawRectangleLines(panelX - 5, panelY - 5, panelW + 10, lineH * 10 + 10, new Color(100,100,100,255));

        int y = panelY;
        Raylib.DrawText("── STATS ──", panelX + 50, y, 18, Color.Gold); y += lineH + 4;

        string vocName = stats.Vocation switch
        {
            1 => "Knight", 2 => "Paladin", 3 => "Sorcerer", 4 => "Druid", _ => "None"
        };
        Raylib.DrawText($"Level: {stats.Level}",              panelX, y, 16, Color.White); y += lineH;
        Raylib.DrawText($"Voc: {vocName}",                    panelX, y, 16, Color.White); y += lineH;
        Raylib.DrawText($"HP: {stats.CurrentHP}/{stats.MaxHP}", panelX, y, 16, new Color(220,50,50,255)); y += lineH;
        Raylib.DrawText($"MP: {stats.CurrentMP}/{stats.MaxMP}", panelX, y, 16, new Color(50,100,220,255)); y += lineH;
        Raylib.DrawText($"Exp: {stats.Experience}",           panelX, y, 16, Color.White); y += lineH;
        Raylib.DrawText($"Next: {stats.ExpToNext}",           panelX, y, 16, Color.Gray);  y += lineH;
        Raylib.DrawText($"Cap: {stats.Capacity}/{stats.MaxCapacity}", panelX, y, 16, Color.White); y += lineH;
        Raylib.DrawText($"Soul: {stats.Soul}",                panelX, y, 16, Color.White); y += lineH;
        int staminaH = stats.Stamina / 60, staminaM = stats.Stamina % 60;
        Raylib.DrawText($"Stamina: {staminaH}h{staminaM:D2}m", panelX, y, 16, Color.White);
    }

    private static void DrawSkillsPanel(in SkillsDataComponent skills, int sw, int sh)
    {
        int panelW = 220, panelX = sw - 230, panelY = 250, lineH = 20;
        Raylib.DrawRectangle(panelX - 5, panelY - 5, panelW + 10, lineH * 10 + 10, new Color(0,0,0,180));
        Raylib.DrawRectangleLines(panelX - 5, panelY - 5, panelW + 10, lineH * 10 + 10, new Color(100,100,100,255));

        int y = panelY;
        Raylib.DrawText("── SKILLS ──", panelX + 50, y, 18, Color.Gold); y += lineH + 4;
        DrawSkillLine("Fist",      skills.FistLevel,      skills.FistPercent,      panelX, ref y, lineH);
        DrawSkillLine("Club",      skills.ClubLevel,      skills.ClubPercent,      panelX, ref y, lineH);
        DrawSkillLine("Sword",     skills.SwordLevel,     skills.SwordPercent,     panelX, ref y, lineH);
        DrawSkillLine("Axe",       skills.AxeLevel,       skills.AxePercent,       panelX, ref y, lineH);
        DrawSkillLine("Distance",  skills.DistanceLevel,  skills.DistancePercent,  panelX, ref y, lineH);
        DrawSkillLine("Shielding", skills.ShieldingLevel, skills.ShieldingPercent, panelX, ref y, lineH);
        DrawSkillLine("Fishing",   skills.FishingLevel,   skills.FishingPercent,   panelX, ref y, lineH);
        DrawSkillLine("Magic Lv",  skills.MagicLevel,     skills.MagicPercent,     panelX, ref y, lineH);
    }

    private static void DrawSkillLine(string name, int level, int percent,
                                      int x, ref int y, int lineH)
    {
        Raylib.DrawText($"{name}: {level}", x, y, 14, Color.White);
        int barX = x + 130, barW = 60, barH = 10;
        Raylib.DrawRectangle(barX, y + 3, barW, barH, new Color(40,40,40,255));
        Raylib.DrawRectangle(barX, y + 3, (int)(barW * percent / 100f), barH, new Color(80,180,80,255));
        Raylib.DrawRectangleLines(barX, y + 3, barW, barH, new Color(100,100,100,255));
        y += lineH;
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
