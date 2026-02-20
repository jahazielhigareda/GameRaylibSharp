namespace Shared;

public static class Constants
{
    public const int    ServerPort     = 9050;
    public const string ServerAddress  = "127.0.0.1";
    public const int    TickRate       = 60;
    public const int    MaxPlayers     = 16;

    // Tile-based movement (estilo Tibia)
    public const int    TileSize       = 32;
    public const int    PlayerSize     = 28;
    public const float  BaseSpeed      = 150f;
    public const float  DiagonalFactor = 1.414f;

    // Mapa
    public const int    MapWidth       = 50;
    public const int    MapHeight      = 38;

    // Frustum Culling (Tibia-style viewport)
    public const int ViewportTilesX = 15;  // Tiles visibles horizontalmente
    public const int ViewportTilesY = 11;  // Tiles visibles verticalmente
    public const int ViewportMargin = 2;   // Margen extra para pre-renderizar

    // ========== STATS SYSTEM (Tibia-style) ==========

    // Nivel inicial y máximo
    public const int    StartLevel     = 1;
    public const int    MaxLevel       = 200;

    // HP base y ganancia por nivel (varía por vocación)
    public const int    BaseHP         = 150;
    public const int    HPPerLevel     = 15;  // Sin vocación

    // MP base y ganancia por nivel
    public const int    BaseMP         = 50;
    public const int    MPPerLevel     = 5;   // Sin vocación

    // Capacidad base
    public const int    BaseCap        = 400;
    public const int    CapPerLevel    = 10;

    // Regeneración (cada N segundos)
    public const float  RegenInterval  = 1.0f;  // Cada 1 segundo
    public const int    HPRegenBase    = 1;      // HP regenerado por tick
    public const int    MPRegenBase    = 1;      // MP regenerado por tick

    // Soul points
    public const int    MaxSoul        = 100;
    public const int    StartSoul      = 100;

    // Stamina (en minutos)
    public const int    MaxStamina     = 2520;   // 42 horas
    public const int    StartStamina   = 2520;

    // ========== SKILLS SYSTEM (Tibia-style) ==========
    // Multiplicador base de tries para subir skill
    public const int    SkillBaseTriesMultiplier = 50;

    // Skill levels iniciales
    public const int    StartSkillLevel = 10;
    public const int    MaxSkillLevel   = 125;

    // ========== EXPERIENCE FORMULA (Tibia-style) ==========
    /// <summary>
    /// Experiencia necesaria para alcanzar un nivel dado.
    /// Fórmula de Tibia: 50/3 * (L^3 - 6*L^2 + 17*L - 12)
    /// </summary>
    public static long ExperienceForLevel(int level)
    {
        if (level <= 1) return 0;
        long l = level;
        return (50 * (l * l * l - 6 * l * l + 17 * l - 12)) / 3;
    }

    /// <summary>
    /// Experiencia necesaria para pasar del nivel actual al siguiente.
    /// </summary>
    public static long ExperienceToNextLevel(int currentLevel)
    {
        return ExperienceForLevel(currentLevel + 1) - ExperienceForLevel(currentLevel);
    }

    /// <summary>
    /// Tries necesarios para subir un skill de un nivel al siguiente.
    /// Fórmula simplificada de Tibia.
    /// </summary>
    public static long SkillTriesForLevel(int skillLevel, float vocationMultiplier = 1.0f)
    {
        return (long)(SkillBaseTriesMultiplier * MathF.Pow(1.1f, skillLevel) * vocationMultiplier);
    }

    public static float StepDuration(float speed, bool diagonal = false)
    {
        float base_duration = TileSize / speed;
        return diagonal ? base_duration * DiagonalFactor : base_duration;
    }
}
