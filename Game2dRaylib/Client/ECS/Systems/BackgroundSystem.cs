using Client.ECS.Systems;
using Raylib_cs;

namespace Client.ECS.Systems;

public class BackgroundSystem : ISystem
{
    private const int GridSize = 40;

    public void Update(float deltaTime)
    {
        int w = Raylib.GetScreenWidth();
        int h = Raylib.GetScreenHeight();

        for (int x = 0; x < w; x += GridSize)
            Raylib.DrawLine(x, 0, x, h, new Color(50, 50, 50, 255));
        for (int y = 0; y < h; y += GridSize)
            Raylib.DrawLine(0, y, w, y, new Color(50, 50, 50, 255));
    }
}
