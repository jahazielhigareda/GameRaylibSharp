using Client.ECS.Components;
using Raylib_cs;
using Shared;

namespace Client.ECS.Systems;

/// <summary>
/// Dibuja el fondo del mapa como una grilla de tiles.
/// Centra la cámara en el jugador local.
/// </summary>
public class BackgroundSystem : ISystem
{
    private readonly World _world;

    public BackgroundSystem(World world) => _world = world;

    public void Update(float deltaTime)
    {
        var (offsetX, offsetY) = GetCameraOffset();

        int ts = Constants.TileSize;
        var walkableColor  = new Color(60, 60, 60, 255);
        var wallColor      = new Color(30, 30, 30, 255);
        var gridColor      = new Color(80, 80, 80, 255);

        for (int x = 0; x < Constants.MapWidth; x++)
        for (int y = 0; y < Constants.MapHeight; y++)
        {
            int screenX = (int)(x * ts + offsetX);
            int screenY = (int)(y * ts + offsetY);

            // Solo dibujar tiles visibles
            if (screenX + ts < 0 || screenX > Raylib.GetScreenWidth() ||
                screenY + ts < 0 || screenY > Raylib.GetScreenHeight())
                continue;

            bool isWall = x == 0 || x == Constants.MapWidth - 1 ||
                          y == 0 || y == Constants.MapHeight - 1;

            Raylib.DrawRectangle(screenX, screenY, ts, ts, isWall ? wallColor : walkableColor);
            Raylib.DrawRectangleLines(screenX, screenY, ts, ts, gridColor);
        }
    }

    public (float offsetX, float offsetY) GetCameraOffset()
    {
        var localPlayer = _world.GetEntitiesWith<LocalPlayerComponent>().FirstOrDefault();
        if (localPlayer == null) return (0, 0);

        var pos = localPlayer.GetComponent<PositionComponent>();
        float centerX = Raylib.GetScreenWidth() / 2f;
        float centerY = Raylib.GetScreenHeight() / 2f;

        return (centerX - pos.X - Constants.TileSize / 2f,
                centerY - pos.Y - Constants.TileSize / 2f);
    }
}
