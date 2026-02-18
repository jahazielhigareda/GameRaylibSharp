namespace Server.ECS.Components;

/// <summary>
/// Posición lógica en tiles y posición visual en píxeles.
/// La posición visual se interpola hacia la posición lógica.
/// </summary>
public class PositionComponent
{
    // Posición lógica (en tiles)
    public int TileX { get; set; }
    public int TileY { get; set; }

    // Posición visual (en píxeles, para interpolación suave)
    public float VisualX { get; set; }
    public float VisualY { get; set; }

    // Posición visual anterior (para interpolar desde)
    public float PrevVisualX { get; set; }
    public float PrevVisualY { get; set; }

    // Progreso de interpolación (0.0 a 1.0)
    public float MoveProgress { get; set; } = 1f;

    // Duración del paso actual
    public float StepDuration { get; set; }

    // ¿Está actualmente en movimiento?
    public bool IsMoving => MoveProgress < 1f;

    public void SetTilePosition(int tx, int ty)
    {
        TileX   = tx;
        TileY   = ty;
        VisualX = tx * Shared.Constants.TileSize;
        VisualY = ty * Shared.Constants.TileSize;
        PrevVisualX = VisualX;
        PrevVisualY = VisualY;
        MoveProgress = 1f;
    }

    /// <summary>
    /// Inicia un movimiento hacia un nuevo tile con interpolación visual.
    /// </summary>
    public void StartMoveTo(int newTileX, int newTileY, float stepDuration)
    {
        PrevVisualX  = TileX * Shared.Constants.TileSize;
        PrevVisualY  = TileY * Shared.Constants.TileSize;
        TileX        = newTileX;
        TileY        = newTileY;
        StepDuration = stepDuration;
        MoveProgress = 0f;
    }

    /// <summary>
    /// Actualiza la interpolación visual.
    /// </summary>
    public void UpdateVisual(float deltaTime)
    {
        if (MoveProgress >= 1f) return;

        MoveProgress += deltaTime / StepDuration;
        if (MoveProgress > 1f) MoveProgress = 1f;

        float targetX = TileX * Shared.Constants.TileSize;
        float targetY = TileY * Shared.Constants.TileSize;

        VisualX = PrevVisualX + (targetX - PrevVisualX) * MoveProgress;
        VisualY = PrevVisualY + (targetY - PrevVisualY) * MoveProgress;
    }
}
