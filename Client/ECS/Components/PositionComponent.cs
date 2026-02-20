namespace Client.ECS.Components;

/// <summary>
/// Arch struct component – client-side position with smooth interpolation.
/// </summary>
public struct PositionComponent
{
    public int   TileX;
    public int   TileY;

    // Visual (pixel) positions
    public float X;
    public float Y;
    public float TargetX;
    public float TargetY;
    public float LerpSpeed;

    public PositionComponent() { LerpSpeed = 10f; }

    public void SetFromServer(int tileX, int tileY, float serverX, float serverY)
    {
        TileX   = tileX;
        TileY   = tileY;
        TargetX = serverX;
        TargetY = serverY;
    }

    public void SnapToTarget() { X = TargetX; Y = TargetY; }

    public void Interpolate(float deltaTime)
    {
        float t = MathF.Min(1f, LerpSpeed * deltaTime);
        X += (TargetX - X) * t;
        Y += (TargetY - Y) * t;
    }
}
