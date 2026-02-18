namespace Client.ECS.Components;

/// <summary>
/// Posición del jugador en el cliente.
/// Incluye posición lógica (tiles) y visual (píxeles con interpolación).
/// </summary>
public class PositionComponent
{
    // Posición lógica en tiles (recibida del servidor)
    public int TileX { get; set; }
    public int TileY { get; set; }

    // Posición visual en píxeles (interpolada suavemente)
    public float X { get; set; }
    public float Y { get; set; }

    // Posición visual objetivo
    public float TargetX { get; set; }
    public float TargetY { get; set; }

    /// <summary>
    /// Velocidad de interpolación visual. Mayor = más rápido alcanza el target.
    /// </summary>
    public float LerpSpeed { get; set; } = 10f;

    public void SetFromServer(int tileX, int tileY, float serverX, float serverY)
    {
        TileX   = tileX;
        TileY   = tileY;
        TargetX = serverX;
        TargetY = serverY;
    }

    /// <summary>
    /// Snap inmediato (para la primera vez o teleport).
    /// </summary>
    public void SnapToTarget()
    {
        X = TargetX;
        Y = TargetY;
    }

    /// <summary>
    /// Interpola suavemente hacia la posición objetivo.
    /// </summary>
    public void Interpolate(float deltaTime)
    {
        float t = MathF.Min(1f, LerpSpeed * deltaTime);
        X += (TargetX - X) * t;
        Y += (TargetY - Y) * t;
    }
}
