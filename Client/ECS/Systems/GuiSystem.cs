using Arch.Core;
using Client.ECS.Components;
using Client.GUI;
using Raylib_cs;

namespace Client.ECS.Systems;

/// <summary>
/// TASK-030 – Draggable GUI window system (Task 8.3).
///
/// Creates the Stats, Skills and Battle List windows on the first frame,
/// then drives <see cref="GuiWindowManager"/> each tick (Update + Render).
///
/// Key bindings:
///   K – toggle Skills window visibility (matches legacy HUD behaviour)
/// </summary>
public class GuiSystem : ISystem
{
    private readonly GuiWindowManager _manager;
    private readonly ClientWorld      _world;

    private GuiWindow? _skillsWindow;
    private bool       _initialized;

    // Arch queries
    private static readonly QueryDescription LocalStatsQuery = new QueryDescription()
        .WithAll<LocalPlayerComponent, StatsDataComponent>();

    private static readonly QueryDescription LocalSkillsQuery = new QueryDescription()
        .WithAll<LocalPlayerComponent, SkillsDataComponent>();

    private static readonly QueryDescription BattleListQuery = new QueryDescription()
        .WithAll<NetworkIdComponent, CreatureClientTag, CreatureHpComponent>();

    public GuiSystem(GuiWindowManager manager, ClientWorld world)
    {
        _manager = manager;
        _world   = world;
    }

    public void Update(float deltaTime)
    {
        if (!_initialized) Initialize();

        // K toggles Skills window (preserves legacy key binding)
        if (Raylib.IsKeyPressed(KeyboardKey.K) && _skillsWindow != null)
            _skillsWindow.Visible = !_skillsWindow.Visible;

        _manager.Update();
        _manager.Render();
    }

    // ── Initialization ────────────────────────────────────────────────────

    private void Initialize()
    {
        _initialized = true;

        int sw = Raylib.GetScreenWidth();

        // Stats window — top-right corner
        var statsWin = new GuiWindow("Stats", sw - 220, 10, 210, 220);
        statsWin.DrawContent = (x, y, w, h) => DrawStats(x, y);
        _manager.AddWindow(statsWin);

        // Skills window — below stats
        _skillsWindow = new GuiWindow("Skills", sw - 220, 240, 210, 200);
        _skillsWindow.DrawContent = (x, y, w, h) => DrawSkills(x, y);
        _manager.AddWindow(_skillsWindow);

        // Battle List window — below skills
        var battleWin = new GuiWindow("Battle List", sw - 220, 450, 210, 140);
        battleWin.DrawContent = (x, y, w, h) => DrawBattleList(x, y, w);
        _manager.AddWindow(battleWin);
    }

    // ── Content drawers ───────────────────────────────────────────────────

    private void DrawStats(int x, int y)
    {
        StatsDataComponent stats = default;
        bool found = false;
        _world.World.Query(in LocalStatsQuery,
            (ref StatsDataComponent s) => { stats = s; found = true; });

        if (!found)
        {
            Raylib.DrawText("Waiting for server...", x, y, 12, Color.Gray);
            return;
        }

        string vocName = stats.Vocation switch
        {
            1 => "Knight", 2 => "Paladin", 3 => "Sorcerer", 4 => "Druid", _ => "None"
        };

        int lineH = 20;
        Raylib.DrawText($"Level: {stats.Level}",                       x, y, 14, Color.White);               y += lineH;
        Raylib.DrawText($"Vocation: {vocName}",                        x, y, 14, Color.White);               y += lineH;
        Raylib.DrawText($"HP: {stats.CurrentHP}/{stats.MaxHP}",        x, y, 14, new Color(220, 50,  50, 255)); y += lineH;
        Raylib.DrawText($"MP: {stats.CurrentMP}/{stats.MaxMP}",        x, y, 14, new Color(50, 100, 220, 255)); y += lineH;
        Raylib.DrawText($"Exp: {stats.Experience}",                    x, y, 14, Color.White);               y += lineH;
        Raylib.DrawText($"Next: {stats.ExpToNext}",                    x, y, 14, Color.Gray);                y += lineH;
        Raylib.DrawText($"Cap: {stats.Capacity}/{stats.MaxCapacity}",  x, y, 14, Color.White);               y += lineH;
        Raylib.DrawText($"Soul: {stats.Soul}",                         x, y, 14, Color.White);               y += lineH;
        int staminaH = stats.Stamina / 60, staminaM = stats.Stamina % 60;
        Raylib.DrawText($"Stamina: {staminaH}h{staminaM:D2}m",         x, y, 14, Color.White);
    }

    private void DrawSkills(int x, int y)
    {
        SkillsDataComponent skills = default;
        bool found = false;
        _world.World.Query(in LocalSkillsQuery,
            (ref SkillsDataComponent s) => { skills = s; found = true; });

        if (!found)
        {
            Raylib.DrawText("Waiting for server...", x, y, 12, Color.Gray);
            return;
        }

        int lineH = 22;
        DrawSkillRow("Fist",      skills.FistLevel,      skills.FistPercent,      x, ref y, lineH);
        DrawSkillRow("Club",      skills.ClubLevel,      skills.ClubPercent,      x, ref y, lineH);
        DrawSkillRow("Sword",     skills.SwordLevel,     skills.SwordPercent,     x, ref y, lineH);
        DrawSkillRow("Axe",       skills.AxeLevel,       skills.AxePercent,       x, ref y, lineH);
        DrawSkillRow("Distance",  skills.DistanceLevel,  skills.DistancePercent,  x, ref y, lineH);
        DrawSkillRow("Shielding", skills.ShieldingLevel, skills.ShieldingPercent, x, ref y, lineH);
        DrawSkillRow("Fishing",   skills.FishingLevel,   skills.FishingPercent,   x, ref y, lineH);
        DrawSkillRow("Magic Lv",  skills.MagicLevel,     skills.MagicPercent,     x, ref y, lineH);
    }

    private static void DrawSkillRow(string name, int level, int percent,
                                     int x, ref int y, int lineH)
    {
        Raylib.DrawText($"{name}: {level}", x, y, 13, Color.White);
        int barX = x + 110, barW = 60, barH = 10;
        Raylib.DrawRectangle(barX, y + 2, barW, barH, new Color(40, 40, 40, 255));
        Raylib.DrawRectangle(barX, y + 2, (int)(barW * percent / 100f), barH,
            new Color(80, 180, 80, 255));
        Raylib.DrawRectangleLines(barX, y + 2, barW, barH, new Color(100, 100, 100, 255));
        y += lineH;
    }

    private void DrawBattleList(int x, int y, int w)
    {
        const int MaxRows = 6;
        const int LineH   = 20;
        int count = 0;

        _world.World.Query(in BattleListQuery,
            (ref NetworkIdComponent nid, ref CreatureHpComponent hp) =>
        {
            if (count >= MaxRows) return;

            Color hpColor = hp.HpPct switch
            {
                >= 70 => new Color(50,  200, 50,  255),
                >= 30 => new Color(220, 180, 30,  255),
                _     => new Color(200, 50,  50,  255)
            };

            Raylib.DrawText($"Creature #{nid.Id}", x, y, 12, Color.LightGray);

            // HP bar on the right side of the row
            int barX = x + w - 52;
            Raylib.DrawRectangle(barX, y + 2, 50, 10, new Color(40, 40, 40, 255));
            Raylib.DrawRectangle(barX, y + 2, (int)(50 * hp.HpPct / 100f), 10, hpColor);
            Raylib.DrawRectangleLines(barX, y + 2, 50, 10, new Color(80, 80, 80, 255));

            y += LineH;
            count++;
        });

        if (count == 0)
            Raylib.DrawText("No creatures nearby", x, y, 12, Color.Gray);
    }
}
