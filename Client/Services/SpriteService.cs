using System.Numerics;
using Client.ECS.Components;
using Raylib_cs;
using Shared;

namespace Client.Services;

/// <summary>
/// Manages the sprite atlas used by CreatureRenderSystem.
///
/// A procedural placeholder atlas is generated at startup when no external
/// image file is present.  The atlas layout is:
///
///   ┌──────────────────────────────┐
///   │ col → 0    1    2            │
///   │ row 0  N-idle N-step1 N-step2│  ← player (OutfitId 0) N
///   │ row 1  E-idle E-step1 E-step2│                          E
///   │ row 2  S-idle S-step1 S-step2│                          S
///   │ row 3  W-idle W-step1 W-step2│                          W
///   │ row 4  N…                    │  ← creature (OutfitId 1) N
///   │ row 5  E…                    │                          E
///   │ row 6  S…                    │                          S
///   │ row 7  W…                    │                          W
///   └──────────────────────────────┘
///
/// Atlas size: 96 × 256 px (3 frames × 32 | 8 rows × 32).
///
/// Call <see cref="Initialize"/> once after <c>Raylib.InitWindow()</c>.
/// </summary>
public sealed class SpriteService : IDisposable
{
    // ── Atlas constants ───────────────────────────────────────────────────
    public const int FrameW       = Constants.TileSize;   // 32 px
    public const int FrameH       = Constants.TileSize;   // 32 px
    public const int FramesPerDir = 3;                    // idle + 2 walk steps
    public const int Directions   = 4;                    // N, E, S, W
    public const int EntityTypes  = 2;                    // 0 = player, 1 = creature
    public const int AtlasW       = FrameW * FramesPerDir;             // 96
    public const int AtlasH       = FrameH * Directions * EntityTypes; // 256

    // ── State ─────────────────────────────────────────────────────────────
    private Texture2D _atlas;
    private bool      _initialized;
    private bool      _disposed;

    public bool IsInitialized => _initialized;

    // ── Initialization ────────────────────────────────────────────────────

    /// <summary>
    /// Generates the procedural sprite atlas and uploads it to the GPU.
    /// Must be called after <c>Raylib.InitWindow()</c>.
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;

        var image = Raylib.GenImageColor(AtlasW, AtlasH, new Color(0, 0, 0, 0));

        // Draw player sprites (entity type 0, rows 0-3)
        DrawEntitySprites(ref image, entityType: 0,
            bodyColor : new Color(50,  100, 200, 255),
            headColor : new Color(255, 200, 120, 255),
            legsColor : new Color(40,  50,  130, 255),
            feetColor : new Color(90,  55,  25,  255));

        // Draw creature sprites (entity type 1, rows 4-7)
        DrawEntitySprites(ref image, entityType: 1,
            bodyColor : new Color(140, 40,  40,  255),
            headColor : new Color(180, 60,  60,  255),
            legsColor : new Color(100, 30,  30,  255),
            feetColor : new Color(80,  20,  20,  255));

        _atlas       = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);
        _initialized = true;
    }

    // ── Public rendering API ──────────────────────────────────────────────

    /// <summary>
    /// Draws one sprite frame at <paramref name="destX"/>, <paramref name="destY"/>,
    /// tinted by <paramref name="tint"/> (use <see cref="Color.White"/> for no tint).
    /// Falls back to a colored rectangle when the atlas is not yet initialised.
    /// </summary>
    public void DrawSprite(int entityType, int cardinalDir, int walkFrame,
                           int destX, int destY, Color tint)
    {
        if (!_initialized) return;

        var src  = GetSourceRect(entityType, cardinalDir, walkFrame);
        var dest = new Rectangle(destX, destY, FrameW, FrameH);
        Raylib.DrawTexturePro(_atlas, src, dest, Vector2.Zero, 0f, tint);
    }

    /// <summary>
    /// Draws a sprite using outfit colorisation: the base sprite is always tinted
    /// by the outfit's <see cref="OutfitComponent.BodyColor"/>.
    /// The head, legs and feet regions are drawn on top as separate tinted passes
    /// using the corresponding outfit colors.
    /// </summary>
    public void DrawSpriteWithOutfit(
        int entityType, int cardinalDir, int walkFrame,
        int destX, int destY,
        in OutfitComponent outfit)
    {
        if (!_initialized) return;

        var src  = GetSourceRect(entityType, cardinalDir, walkFrame);
        var dest = new Rectangle(destX, destY, FrameW, FrameH);

        // Pass 1 – full sprite tinted by body colour (covers body + legs + feet)
        Raylib.DrawTexturePro(_atlas, src, dest, Vector2.Zero, 0f, outfit.BodyColor);

        // Pass 2 – head overlay (top quarter of the frame, tinted by head colour)
        var headSrc  = src with { Height = FrameH / 3f };
        var headDest = dest with { Height = dest.Height / 3f };
        Raylib.DrawTexturePro(_atlas, headSrc, headDest, Vector2.Zero, 0f, outfit.HeadColor);

        // Pass 3 – legs overlay (middle third, tinted by legs colour)
        float legOffset = FrameH / 3f;
        var legSrc   = src  with { Y = src.Y  + legOffset, Height = FrameH / 3f };
        var legDest  = dest with { Y = dest.Y + legOffset, Height = dest.Height / 3f };
        Raylib.DrawTexturePro(_atlas, legSrc, legDest, Vector2.Zero, 0f, outfit.LegsColor);

        // Pass 4 – feet overlay (bottom third, tinted by feet colour)
        float feetOffset = FrameH * 2f / 3f;
        var feetSrc  = src  with { Y = src.Y  + feetOffset, Height = FrameH / 3f };
        var feetDest = dest with { Y = dest.Y + feetOffset, Height = dest.Height / 3f };
        Raylib.DrawTexturePro(_atlas, feetSrc, feetDest, Vector2.Zero, 0f, outfit.FeetColor);
    }

    // ── Atlas helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the source rectangle inside the atlas for the given
    /// entity type (0=player, 1=creature), cardinal direction (0=N…3=W)
    /// and walk frame (0=idle, 1-2=step).
    /// </summary>
    public static Rectangle GetSourceRect(int entityType, int cardinalDir, int walkFrame)
    {
        int col = Math.Clamp(walkFrame,    0, FramesPerDir - 1);
        int row = Math.Clamp(entityType, 0, EntityTypes   - 1) * Directions
                + Math.Clamp(cardinalDir,  0, Directions  - 1);
        return new Rectangle(col * FrameW, row * FrameH, FrameW, FrameH);
    }

    // ── Procedural sprite drawing ─────────────────────────────────────────

    private static void DrawEntitySprites(
        ref Image image, int entityType,
        Color bodyColor, Color headColor, Color legsColor, Color feetColor)
    {
        int typeRowBase = entityType * Directions;

        for (int dir = 0; dir < Directions; dir++)
        {
            int rowY = (typeRowBase + dir) * FrameH;
            for (int frame = 0; frame < FramesPerDir; frame++)
            {
                int colX = frame * FrameW;
                DrawCharacterFrame(ref image, colX, rowY, dir, frame,
                    bodyColor, headColor, legsColor, feetColor);
            }
        }
    }

    /// <summary>
    /// Draws one 32×32 character frame into the image at (<paramref name="ox"/>, <paramref name="oy"/>).
    ///
    /// Vertical layout of a 32-px tall frame:
    ///   Head region  – rows  0-10  (head circle)
    ///   Body region  – rows 11-21  (torso rectangle)
    ///   Legs region  – rows 22-27  (upper leg rectangles)
    ///   Feet region  – rows 28-31  (boot rectangles, animated by walk frame)
    ///
    /// A direction indicator dot is drawn at the edge facing the character's direction.
    /// </summary>
    private static void DrawCharacterFrame(
        ref Image image,
        int ox, int oy,
        int dirIdx, int walkFrame,
        Color bodyColor, Color headColor, Color legsColor, Color feetColor)
    {
        int ts = FrameW; // 32

        // ── Head ─────────────────────────────────────────────────────────
        Raylib.ImageDrawCircle(ref image, ox + ts / 2, oy + 8, 7, headColor);

        // ── Body ─────────────────────────────────────────────────────────
        Raylib.ImageDrawRectangle(ref image, ox + 9, oy + 14, 14, 9, bodyColor);

        // ── Legs – animate left/right sway based on walk frame ────────────
        int swayL = walkFrame == 1 ?  2 : (walkFrame == 2 ? -2 : 0);
        int swayR = -swayL;
        Raylib.ImageDrawRectangle(ref image, ox + 9,  oy + 22 + swayL, 5, 5, legsColor);
        Raylib.ImageDrawRectangle(ref image, ox + 17, oy + 22 + swayR, 5, 5, legsColor);

        // ── Feet ─────────────────────────────────────────────────────────
        Raylib.ImageDrawRectangle(ref image, ox + 8,  oy + 27, 6, 4, feetColor);
        Raylib.ImageDrawRectangle(ref image, ox + 17, oy + 27, 6, 4, feetColor);

        // ── Direction indicator (dot on the edge the character faces) ─────
        var (dotDx, dotDy) = dirIdx switch
        {
            0 => ( 0, -1),  // North
            1 => ( 1,  0),  // East
            2 => ( 0,  1),  // South
            3 => (-1,  0),  // West
            _ => ( 0,  1),
        };
        int dotX = ox + ts / 2 + dotDx * 8;
        int dotY = oy + ts / 2 + dotDy * 8;
        Raylib.ImageDrawCircle(ref image, dotX, dotY, 3,
            new Color(255, 255, 0, 220));
    }

    // ── IDisposable ───────────────────────────────────────────────────────

    public void Dispose()
    {
        if (!_disposed && _initialized)
        {
            Raylib.UnloadTexture(_atlas);
            _disposed = true;
        }
    }
}
