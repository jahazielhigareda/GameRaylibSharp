using Client.Services;
using Arch.Core;
using Arch.Core.Extensions;
using Client.ECS.Components;
using Raylib_cs;
using Shared;

namespace Client.ECS.Systems;

/// <summary>
/// Layer 5 – renders transient effects (spells, particles, magic glows)
/// and removes expired ones from the ECS world.
/// </summary>
public class EffectRenderSystem : ISystem
{
    private readonly ClientWorld   _world;
    private readonly CameraService _camera;

    private static readonly QueryDescription EffectQuery = new QueryDescription()
        .WithAll<PositionComponent, EffectComponent>();

    private readonly List<Arch.Core.Entity> _toRemove = new();

    public EffectRenderSystem(ClientWorld world, CameraService camera)
    {
        _world  = world;
        _camera = camera;
    }

    public void Update(float deltaTime)
    {
        var (offsetX, offsetY) = _camera.GetOffset();
        int ts = Constants.TileSize;

        _toRemove.Clear();

        _world.World.Query(in EffectQuery,
            (Arch.Core.Entity entity,
             ref PositionComponent pos,
             ref EffectComponent   fx) =>
        {
            fx.Age   += deltaTime;
            fx.Alpha  = Math.Max(0f, 1f - fx.Progress);

            if (fx.IsExpired) { _toRemove.Add(entity); return; }

            int sx = (int)(pos.X + offsetX);
            int sy = (int)(pos.Y + offsetY);

            byte alpha = (byte)(fx.Alpha * 200);

            Color col = fx.EffectId switch
            {
                1    => new Color((byte)255, (byte)80,  (byte)0,   alpha), // fire
                2    => new Color((byte)0,   (byte)120, (byte)255, alpha), // ice
                3    => new Color((byte)160, (byte)0,   (byte)200, alpha), // magic
                4    => new Color((byte)255, (byte)255, (byte)0,   alpha), // lightning
                _    => new Color((byte)255, (byte)255, (byte)255, alpha), // default glow
            };

            // Draw expanding ring
            float scale = 0.5f + fx.Progress * 0.5f;
            int   r     = (int)(ts * scale * 0.5f);
            Raylib.DrawCircle(sx + ts / 2, sy + ts / 2, r, col);
        });

        foreach (var e in _toRemove)
            if (e.IsAlive()) _world.DestroyEntity(e);
    }
}
