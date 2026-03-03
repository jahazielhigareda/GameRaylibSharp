using Shared;

namespace Client.ECS.Components;

/// <summary>
/// Tracks the walk-cycle animation state for creature and player entities.
/// Updated each frame by AnimationSystem before the render pass.
/// </summary>
public struct AnimationComponent
{
    // ── Facing direction ─────────────────────────────────────────────────
    /// <summary>Current facing direction (South by default).</summary>
    public Direction FacingDirection;

    // ── Walk cycle ────────────────────────────────────────────────────────
    /// <summary>Current walk-cycle frame index (0 = idle, 1-2 = walk steps).</summary>
    public int   Frame;
    /// <summary>Seconds elapsed on the current frame.</summary>
    public float FrameTimer;
    /// <summary>Duration of each walk frame in seconds.</summary>
    public float FrameDuration;
    /// <summary>True while the entity is visually moving between tiles.</summary>
    public bool  IsMoving;

    // ── Direction detection bookkeeping ───────────────────────────────────
    /// <summary>Tile-X from the previous update; used to detect movement direction.</summary>
    public int PrevTileX;
    /// <summary>Tile-Y from the previous update; used to detect movement direction.</summary>
    public int PrevTileY;

    public AnimationComponent()
    {
        FacingDirection = Direction.South;
        Frame           = 0;
        FrameTimer      = 0f;
        FrameDuration   = 0.15f;
        IsMoving        = false;
        PrevTileX       = -1;
        PrevTileY       = -1;
    }

    /// <summary>
    /// Advances the animation using the entity's current <see cref="PositionComponent"/>.
    /// Call once per frame from AnimationSystem.
    /// </summary>
    public void Update(float deltaTime, ref PositionComponent pos)
    {
        // Detect tile change to update facing direction
        if (PrevTileX != pos.TileX || PrevTileY != pos.TileY)
        {
            int dx = pos.TileX - PrevTileX;
            int dy = pos.TileY - PrevTileY;
            if (PrevTileX != -1 && (dx != 0 || dy != 0))
                FacingDirection = DirectionHelper.FromOffset(
                    Math.Sign(dx), Math.Sign(dy));

            PrevTileX = pos.TileX;
            PrevTileY = pos.TileY;
        }

        // Consider moving while visual position is still catching up to target
        IsMoving = MathF.Abs(pos.X - pos.TargetX) > 0.5f
                || MathF.Abs(pos.Y - pos.TargetY) > 0.5f;

        if (IsMoving)
        {
            FrameTimer += deltaTime;
            if (FrameTimer >= FrameDuration)
            {
                FrameTimer -= FrameDuration;
                Frame = (Frame + 1) % 3;   // 3-frame walk cycle
            }
        }
        else
        {
            Frame      = 0;
            FrameTimer = 0f;
        }
    }

    /// <summary>
    /// Maps the current <see cref="FacingDirection"/> to a cardinal-direction index
    /// used to look up the correct sprite row in the atlas (0=N, 1=E, 2=S, 3=W).
    /// </summary>
    public readonly int CardinalIndex => FacingDirection switch
    {
        Direction.North                                         => 0,
        Direction.NorthEast or Direction.East or Direction.SouthEast => 1,
        Direction.South                                         => 2,
        Direction.West  or Direction.SouthWest or Direction.NorthWest => 3,
        _                                                       => 2,
    };
}
