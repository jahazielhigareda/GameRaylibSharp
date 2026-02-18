using Client.ECS.Components;
using Client.Services;
using Raylib_cs;

namespace Client.ECS.Systems;

public class HudSystem : ISystem
{
    private readonly World            _world;
    private readonly GameStateService _state;

    // Toggle para mostrar panel de skills (tecla K)
    private bool _showSkills = false;

    public HudSystem(World world, GameStateService state)
    {
        _world = world;
        _state = state;
    }

    public void Update(float deltaTime)
    {
        // Toggle skills panel con tecla K
        if (Raylib.IsKeyPressed(KeyboardKey.K))
            _showSkills = !_showSkills;

        int players = _world.GetEntitiesWith<NetworkIdComponent>().Count();
        int fps     = Raylib.GetFPS();
        int sw      = Raylib.GetScreenWidth();
        int sh      = Raylib.GetScreenHeight();

        // --- Panel superior izquierdo: Info general ---
        Raylib.DrawText($"Players: {players}",         10, 10, 20, Color.White);
        Raylib.DrawText($"Server tick: {_state.Tick}",  10, 35, 20, Color.White);
        Raylib.DrawText($"FPS: {fps}",                  10, 60, 20, Color.White);

        var local = _world.GetEntitiesWith<LocalPlayerComponent>().FirstOrDefault();
        if (local == null) return;

        var pos = local.GetComponent<PositionComponent>();
        Raylib.DrawText($"Tile: ({pos.TileX}, {pos.TileY})", 10, 85, 20, Color.Yellow);

        // --- Panel de Stats (derecha) ---
        if (local.HasComponent<StatsDataComponent>())
        {
            var stats = local.GetComponent<StatsDataComponent>();
            DrawStatsPanel(stats, sw, sh);
        }

        // --- Panel de Skills (toggle con K) ---
        if (_showSkills && local.HasComponent<SkillsDataComponent>())
        {
            var skills = local.GetComponent<SkillsDataComponent>();
            DrawSkillsPanel(skills, sw, sh);
        }

        // --- Barras de HP/MP en la parte inferior ---
        if (local.HasComponent<StatsDataComponent>())
        {
            var stats = local.GetComponent<StatsDataComponent>();
            DrawHealthBars(stats, sw, sh);
        }

        Raylib.DrawText("WASD/Flechas: Mover | K: Skills", 10,
            sh - 30, 16, Color.LightGray);
    }

    private void DrawStatsPanel(StatsDataComponent stats, int sw, int sh)
    {
        int panelW = 200;
        int panelX = sw - panelW - 10;
        int panelY = 10;
        int lineH  = 22;

        // Fondo del panel
        Raylib.DrawRectangle(panelX - 5, panelY - 5, panelW + 10, lineH * 10 + 10,
            new Color(0, 0, 0, 180));
        Raylib.DrawRectangleLines(panelX - 5, panelY - 5, panelW + 10, lineH * 10 + 10,
            new Color(100, 100, 100, 255));

        int y = panelY;
        Raylib.DrawText("── STATS ──", panelX + 50, y, 18, Color.Gold);
        y += lineH + 4;

        string vocName = stats.Vocation switch
        {
            1 => "Knight", 2 => "Paladin", 3 => "Sorcerer", 4 => "Druid", _ => "None"
        };

        Raylib.DrawText($"Level: {stats.Level}", panelX, y, 16, Color.White);
        y += lineH;
        Raylib.DrawText($"Voc: {vocName}", panelX, y, 16, Color.White);
        y += lineH;
        Raylib.DrawText($"HP: {stats.CurrentHP}/{stats.MaxHP}", panelX, y, 16,
            new Color(220, 50, 50, 255));
        y += lineH;
        Raylib.DrawText($"MP: {stats.CurrentMP}/{stats.MaxMP}", panelX, y, 16,
            new Color(50, 100, 220, 255));
        y += lineH;
        Raylib.DrawText($"Exp: {stats.Experience}", panelX, y, 16, Color.White);
        y += lineH;
        Raylib.DrawText($"Next: {stats.ExpToNext}", panelX, y, 16, Color.Gray);
        y += lineH;
        Raylib.DrawText($"Cap: {stats.Capacity}/{stats.MaxCapacity}", panelX, y, 16, Color.White);
        y += lineH;
        Raylib.DrawText($"Soul: {stats.Soul}", panelX, y, 16, Color.White);
        y += lineH;

        int staminaH = stats.Stamina / 60;
        int staminaM = stats.Stamina % 60;
        Raylib.DrawText($"Stamina: {staminaH}h{staminaM:D2}m", panelX, y, 16, Color.White);
    }

    private void DrawSkillsPanel(SkillsDataComponent skills, int sw, int sh)
    {
        int panelW = 220;
        int panelX = sw - panelW - 10;
        int panelY = 250;
        int lineH  = 20;

        // Fondo
        Raylib.DrawRectangle(panelX - 5, panelY - 5, panelW + 10, lineH * 10 + 10,
            new Color(0, 0, 0, 180));
        Raylib.DrawRectangleLines(panelX - 5, panelY - 5, panelW + 10, lineH * 10 + 10,
            new Color(100, 100, 100, 255));

        int y = panelY;
        Raylib.DrawText("── SKILLS ──", panelX + 50, y, 18, Color.Gold);
        y += lineH + 4;

        DrawSkillLine("Fist",      skills.FistLevel,      skills.FistPercent,      panelX, ref y, lineH);
        DrawSkillLine("Club",      skills.ClubLevel,      skills.ClubPercent,      panelX, ref y, lineH);
        DrawSkillLine("Sword",     skills.SwordLevel,     skills.SwordPercent,     panelX, ref y, lineH);
        DrawSkillLine("Axe",       skills.AxeLevel,       skills.AxePercent,       panelX, ref y, lineH);
        DrawSkillLine("Distance",  skills.DistanceLevel,  skills.DistancePercent,  panelX, ref y, lineH);
        DrawSkillLine("Shielding", skills.ShieldingLevel, skills.ShieldingPercent, panelX, ref y, lineH);
        DrawSkillLine("Fishing",   skills.FishingLevel,   skills.FishingPercent,   panelX, ref y, lineH);
        DrawSkillLine("Magic Lv",  skills.MagicLevel,     skills.MagicPercent,     panelX, ref y, lineH);
    }

    private void DrawSkillLine(string name, int level, int percent, int x, ref int y, int lineH)
    {
        Raylib.DrawText($"{name}: {level}", x, y, 14, Color.White);

        // Barra de progreso pequeña
        int barX = x + 130;
        int barW = 60;
        int barH = 10;
        Raylib.DrawRectangle(barX, y + 3, barW, barH, new Color(40, 40, 40, 255));
        int fillW = (int)(barW * percent / 100f);
        Raylib.DrawRectangle(barX, y + 3, fillW, barH, new Color(80, 180, 80, 255));
        Raylib.DrawRectangleLines(barX, y + 3, barW, barH, new Color(100, 100, 100, 255));

        y += lineH;
    }

    private void DrawHealthBars(StatsDataComponent stats, int sw, int sh)
    {
        int barW = 200;
        int barH = 20;
        int barX = (sw - barW) / 2;
        int hpY  = sh - 60;
        int mpY  = sh - 35;

        // HP Bar
        float hpRatio = stats.MaxHP > 0 ? (float)stats.CurrentHP / stats.MaxHP : 0;
        Raylib.DrawRectangle(barX, hpY, barW, barH, new Color(40, 0, 0, 200));
        Raylib.DrawRectangle(barX, hpY, (int)(barW * hpRatio), barH, new Color(200, 30, 30, 255));
        Raylib.DrawRectangleLines(barX, hpY, barW, barH, new Color(150, 50, 50, 255));
        string hpText = $"{stats.CurrentHP}/{stats.MaxHP}";
        Raylib.DrawText(hpText, barX + barW / 2 - Raylib.MeasureText(hpText, 14) / 2,
            hpY + 3, 14, Color.White);

        // MP Bar
        float mpRatio = stats.MaxMP > 0 ? (float)stats.CurrentMP / stats.MaxMP : 0;
        Raylib.DrawRectangle(barX, mpY, barW, barH, new Color(0, 0, 40, 200));
        Raylib.DrawRectangle(barX, mpY, (int)(barW * mpRatio), barH, new Color(30, 80, 200, 255));
        Raylib.DrawRectangleLines(barX, mpY, barW, barH, new Color(50, 50, 150, 255));
        string mpText = $"{stats.CurrentMP}/{stats.MaxMP}";
        Raylib.DrawText(mpText, barX + barW / 2 - Raylib.MeasureText(mpText, 14) / 2,
            mpY + 3, 14, Color.White);

        // Exp bar (debajo del panel de stats, arriba de las barras)
        int expBarY = hpY - 15;
        int expBarH = 8;
        long totalForLevel = stats.ExpToNext > 0
            ? stats.Experience % (stats.ExpToNext + 1)
            : 0;
        float expRatio = stats.ExpToNext > 0
            ? 1f - ((float)stats.ExpToNext / (float)(stats.ExpToNext + totalForLevel + 1))
            : 0;
        // Clamp
        if (expRatio < 0) expRatio = 0;
        if (expRatio > 1) expRatio = 1;

        Raylib.DrawRectangle(barX, expBarY, barW, expBarH, new Color(20, 20, 20, 200));
        Raylib.DrawRectangle(barX, expBarY, (int)(barW * expRatio), expBarH,
            new Color(180, 180, 30, 255));
        Raylib.DrawRectangleLines(barX, expBarY, barW, expBarH, new Color(100, 100, 50, 255));
    }
}
