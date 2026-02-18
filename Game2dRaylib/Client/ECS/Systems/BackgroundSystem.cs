using Arch.Core.Extensions;
using Client.ECS.Components;
using Raylib_cs;
using Shared;

namespace Client.ECS.Systems;

/// <summary>
/// Arch-based background system.
/// Draws tile grid with Tibia-style frustum culling.
/// </summary>
public class BackgroundSystem : ISystem
{
    private readonly ClientWorld _world;

    public int VisibleMinTileX { get; private set; }
    public int VisibleMaxTileX { get; private set; }
    public int VisibleMinTileY { get; private set; }
    public int VisibleMaxTileY { get; private set; }

    public BackgroundSystem(ClientWorld world) => _world = world;

    public void Update(float deltaTime)
    {
        var (offsetX, offsetY) = GetCameraOffset();

        int ts = Constants.TileSize;
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();

        var walkableColor = new Color(60,  60,  60,  255);
        var wallColor     = new Color(30,  30,  30,  255);
        var gridColor     = new Color(80,  80,  80,  255);

        float worldLeftX   = -offsetX;
        float worldTopY    = -offsetY;
        float worldRightX  = -offsetX + sw;
        float worldBottomY = -offsetY + sh;

        VisibleMinTileX = Math.Max(0,                    (int)MathF.Floor(worldLeftX  / ts) - 1);
        VisibleMinTileY = Math.Max(0,                    (int)MathF.Floor(worldTopY   / ts) - 1);
        VisibleMaxTileX = Math.Min(Constants.MapWidth-1, (int)MathF.Floor(worldRightX  / ts) + 1);
        VisibleMaxTileY = Math.Min(Constants.MapHeight-1,(int)MathF.Floor(worldBottomY / ts) + 1);

        for (int x = VisibleMinTileX; x <= VisibleMaxTileX; x++)
        for (int y = VisibleMinTileY; y <= VisibleMaxTileY; y++)
        {
            int screenX = (int)(x * ts + offsetX);
            int screenY = (int)(y * ts + offsetY);

            bool isWall = x == 0 || x == Constants.MapWidth - 1 ||
                          y == 0 || y == Constants.MapHeight - 1;

            Raylib.DrawRectangle(screenX, screenY, ts, ts, isWall ? wallColor : walkableColor);
            Raylib.DrawRectangleLines(screenX, screenY, ts, ts, gridColor);
        }
    }

    public (float offsetX, float offsetY) GetCameraOffset()
    {
        if (!_world.TryGetLocalPlayer(out var entity)) return (0, 0);
        ref var pos = ref entity.Get<PositionComponent>();
        float centerX = Raylib.GetScreenWidth()  / 2f;
        float centerY = Raylib.GetScreenHeight() / 2f;
        return (centerX - pos.X - Constants.TileSize / 2f,
                centerY - pos.Y - Constants.TileSize / 2f);
    }

    public bool IsTileInViewport(int tileX, int tileY)
        => tileX >= VisibleMinTileX && tileX <= VisibleMaxTileX &&
           tileY >= VisibleMinTileY && tileY <= VisibleMaxTileY;
}
