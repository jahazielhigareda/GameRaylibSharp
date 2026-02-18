namespace Shared;

public static class Constants
{
    public const int    ServerPort     = 9050;
    public const string ServerAddress  = "127.0.0.1";
    public const int    TickRate       = 60;
    public const int    MaxPlayers     = 16;

    // Tile-based movement (estilo Tibia)
    public const int    TileSize       = 32;       // Tamaño de cada tile en píxeles
    public const int    PlayerSize     = 28;       // Tamaño visual del jugador (menor que tile)
    public const float  BaseSpeed      = 150f;     // Velocidad base del personaje (píxeles/s)
    public const float  DiagonalFactor = 1.414f;   // Factor de costo diagonal (√2)

    // Mapa
    public const int    MapWidth       = 50;       // Ancho del mapa en tiles
    public const int    MapHeight      = 38;       // Alto del mapa en tiles

    /// <summary>
    /// Calcula la duración de un paso en segundos dado un speed.
    /// stepDuration = tileSize / speed
    /// </summary>
    public static float StepDuration(float speed, bool diagonal = false)
    {
        float base_duration = TileSize / speed;
        return diagonal ? base_duration * DiagonalFactor : base_duration;
    }
}
