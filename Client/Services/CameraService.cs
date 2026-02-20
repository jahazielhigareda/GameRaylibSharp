using Arch.Core.Extensions;
using Client.ECS;
using Client.ECS.Components;
using Raylib_cs;
using Shared;

namespace Client.Services;

/// <summary>
/// Computes the camera world→screen offset from the local player position.
/// Extracted from BackgroundSystem so all render systems share one source.
/// </summary>
public sealed class CameraService
{
    private readonly ClientWorld _world;

    public CameraService(ClientWorld world) => _world = world;

    public (float offsetX, float offsetY) GetOffset()
    {
        if (!_world.TryGetLocalPlayer(out var entity))
            return (0f, 0f);

        ref var pos = ref entity.Get<PositionComponent>();
        float cx = Raylib.GetScreenWidth()  / 2f;
        float cy = Raylib.GetScreenHeight() / 2f;
        return (cx - pos.X - Constants.TileSize / 2f,
                cy - pos.Y - Constants.TileSize / 2f);
    }

    // Visible tile range helpers
    public (int minTX, int minTY, int maxTX, int maxTY) VisibleTileRange(
        int mapW = int.MaxValue, int mapH = int.MaxValue)
    {
        var (ox, oy) = GetOffset();
        int ts  = Constants.TileSize;
        int sw  = Raylib.GetScreenWidth();
        int sh  = Raylib.GetScreenHeight();

        int minTX = Math.Max(0,    (int)Math.Floor(-ox / ts) - 1);
        int minTY = Math.Max(0,    (int)Math.Floor(-oy / ts) - 1);
        int maxTX = Math.Min(mapW - 1, (int)Math.Floor((-ox + sw) / ts) + 1);
        int maxTY = Math.Min(mapH - 1, (int)Math.Floor((-oy + sh) / ts) + 1);
        return (minTX, minTY, maxTX, maxTY);
    }
}
