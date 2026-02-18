using Client.ECS.Components;
using Raylib_cs;
using Shared;

namespace Client.ECS.Systems;

/// <summary>
/// Dibuja el fondo del mapa como una grilla de tiles.
/// Implementa frustum culling estilo Tibia: calcula exactamente qué tiles
/// son visibles en la ventana y solo renderiza esos.
/// </summary>
public class BackgroundSystem : ISystem
{
    private readonly World _world;

    // Cache del rango visible para que otros sistemas lo consulten
    public int VisibleMinTileX { get; private set; }
    public int VisibleMaxTileX { get; private set; }
    public int VisibleMinTileY { get; private set; }
    public int VisibleMaxTileY { get; private set; }

    public BackgroundSystem(World world) => _world = world;

    public void Update(float deltaTime)
    {
        var (offsetX, offsetY) = GetCameraOffset();

        int ts = Constants.TileSize;
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();

        var walkableColor = new Color(60, 60, 60, 255);
        var wallColor     = new Color(30, 30, 30, 255);
        var gridColor     = new Color(80, 80, 80, 255);

        // === FRUSTUM CULLING ROBUSTO ===
        // Convertir las 4 esquinas de la pantalla a coordenadas de tile.
        // -offsetX es la coordenada mundo de la esquina izquierda de la pantalla.
        // (-offsetX + sw) es la coordenada mundo de la esquina derecha.
        // Floor para el mínimo, Floor para el máximo, luego +1/-1 de margen
        // para cubrir tiles parcialmente visibles en los bordes.

        // Esquina superior-izquierda de la pantalla en coordenadas mundo
        float worldLeftX  = -offsetX;
        float worldTopY   = -offsetY;
        // Esquina inferior-derecha de la pantalla en coordenadas mundo
        float worldRightX  = -offsetX + sw;
        float worldBottomY = -offsetY + sh;

        // Convertir a índices de tile con margen de 1 tile extra en cada lado
        // para cubrir tiles parcialmente visibles y evitar pop-in durante movimiento
        VisibleMinTileX = (int)MathF.Floor(worldLeftX / ts) - 1;
        VisibleMinTileY = (int)MathF.Floor(worldTopY / ts) - 1;
        VisibleMaxTileX = (int)MathF.Floor(worldRightX / ts) + 1;
        VisibleMaxTileY = (int)MathF.Floor(worldBottomY / ts) + 1;

        // Clamp al tamaño del mapa para no dibujar fuera de límites
        VisibleMinTileX = Math.Max(0, VisibleMinTileX);
        VisibleMinTileY = Math.Max(0, VisibleMinTileY);
        VisibleMaxTileX = Math.Min(Constants.MapWidth - 1, VisibleMaxTileX);
        VisibleMaxTileY = Math.Min(Constants.MapHeight - 1, VisibleMaxTileY);

        // Solo iterar sobre tiles visibles
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
        var localPlayer = _world.GetEntitiesWith<LocalPlayerComponent>().FirstOrDefault();
        if (localPlayer == null) return (0, 0);

        var pos = localPlayer.GetComponent<PositionComponent>();
        float centerX = Raylib.GetScreenWidth() / 2f;
        float centerY = Raylib.GetScreenHeight() / 2f;

        return (centerX - pos.X - Constants.TileSize / 2f,
                centerY - pos.Y - Constants.TileSize / 2f);
    }

    /// <summary>
    /// Verifica si un tile está dentro del rango visible calculado.
    /// Más eficiente que convertir píxeles: usa directamente los índices de tile.
    /// </summary>
    public bool IsTileInViewport(int tileX, int tileY)
    {
        return tileX >= VisibleMinTileX && tileX <= VisibleMaxTileX &&
               tileY >= VisibleMinTileY && tileY <= VisibleMaxTileY;
    }

    /// <summary>
    /// Verifica si una posición en píxeles del mundo está dentro del viewport visible.
    /// </summary>
    public bool IsWorldPosInViewport(float worldX, float worldY, float entitySize = 0)
    {
        var (offsetX, offsetY) = GetCameraOffset();
        float screenX = worldX + offsetX;
        float screenY = worldY + offsetY;
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        float margin = Constants.TileSize + entitySize;

        return screenX + entitySize > -margin && screenX < sw + margin &&
               screenY + entitySize > -margin && screenY < sh + margin;
    }
}
