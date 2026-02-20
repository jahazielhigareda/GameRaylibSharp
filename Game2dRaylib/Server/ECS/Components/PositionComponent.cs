using Shared;

namespace Server.ECS.Components;

/// <summary>
/// Arch struct component – tile position + visual interpolation (server-side).
/// </summary>
public struct PositionComponent
{
    // Logical tile position
    public int   TileX;
    public int   TileY;
    public byte  FloorZ;

    // Visual pixel position (interpolated)
    public float VisualX;
    public float VisualY;

    // Previous visual position (interpolate from)
    public float PrevVisualX;
    public float PrevVisualY;

    // Interpolation progress [0..1]
    public float MoveProgress;

    // Step duration in seconds
    public float StepDuration;

    public readonly bool IsMoving => MoveProgress < 1f;

    public void SetTilePosition(int tx, int ty)
    {
        TileX        = tx;
        TileY        = ty;
        VisualX      = tx * Constants.TileSize;
        VisualY      = ty * Constants.TileSize;
        PrevVisualX  = VisualX;
        PrevVisualY  = VisualY;
        MoveProgress = 1f;
    }

    public void StartMoveTo(int newTileX, int newTileY, float stepDuration)
    {
        PrevVisualX  = TileX * Constants.TileSize;
        PrevVisualY  = TileY * Constants.TileSize;
        TileX        = newTileX;
        TileY        = newTileY;
        StepDuration = stepDuration;
        MoveProgress = 0f;
    }

    public void UpdateVisual(float deltaTime)
    {
        if (MoveProgress >= 1f) return;

        MoveProgress += deltaTime / StepDuration;
        if (MoveProgress > 1f) MoveProgress = 1f;

        float targetX = TileX * Constants.TileSize;
        float targetY = TileY * Constants.TileSize;

        VisualX = PrevVisualX + (targetX - PrevVisualX) * MoveProgress;
        VisualY = PrevVisualY + (targetY - PrevVisualY) * MoveProgress;
    }
}
